using DPUruNet;
using System;
using System.Text;

namespace DevsFingerPrint.Infrastructure.Services
{
    public enum FormatoTemplateBiometrico
    {
        AnsiBinario,
        IsoBinario,
        XmlHistorico,
        Desconocido
    }

    public sealed class ResultadoImportacionTemplate
    {
        private ResultadoImportacionTemplate(bool exito, Fmd fmd, FormatoTemplateBiometrico formato, string motivo)
        {
            Exito = exito;
            Fmd = fmd;
            Formato = formato;
            Motivo = motivo;
        }

        public bool Exito { get; private set; }
        public Fmd Fmd { get; private set; }
        public FormatoTemplateBiometrico Formato { get; private set; }
        public string Motivo { get; private set; }

        public static ResultadoImportacionTemplate Ok(Fmd fmd, FormatoTemplateBiometrico formato)
        {
            return new ResultadoImportacionTemplate(true, fmd, formato, "ok");
        }

        public static ResultadoImportacionTemplate Fallo(FormatoTemplateBiometrico formato, string motivo)
        {
            return new ResultadoImportacionTemplate(false, null, formato, motivo);
        }
    }

    public static class TemplateBiometricoImporter
    {
        public const int MinimoBytes = 32;
        public const int MaximoBytes = 262144;
        public const int MaximoCaracteres = 350000;

        public static ResultadoImportacionTemplate TryImportarTemplateBiometrico(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return ResultadoImportacionTemplate.Fallo(FormatoTemplateBiometrico.Desconocido, "vacio");
            }

            string limpio = template.Trim().TrimStart('\uFEFF');
            if (limpio.Length < 8)
            {
                return ResultadoImportacionTemplate.Fallo(FormatoTemplateBiometrico.Desconocido, "demasiado-corto");
            }

            if (limpio.Length > MaximoCaracteres)
            {
                return ResultadoImportacionTemplate.Fallo(FormatoTemplateBiometrico.Desconocido, "demasiado-grande");
            }

            if (PareceXml(limpio))
            {
                return ImportarXml(limpio);
            }

            byte[] bytes;
            if (!TryDecodificarBase64(limpio, out bytes))
            {
                return ResultadoImportacionTemplate.Fallo(FormatoTemplateBiometrico.Desconocido, "base64-invalido");
            }

            if (bytes.Length > MaximoBytes)
            {
                return ResultadoImportacionTemplate.Fallo(FormatoTemplateBiometrico.Desconocido, "demasiado-grande");
            }

            string texto = DecodificarTexto(bytes);
            if (PareceXml(texto))
            {
                return ImportarXml(texto);
            }

            if (bytes.Length < MinimoBytes)
            {
                return ResultadoImportacionTemplate.Fallo(FormatoTemplateBiometrico.Desconocido, "demasiado-corto");
            }

            ResultadoImportacionTemplate ansi = ImportarBinario(bytes, Constants.Formats.Fmd.ANSI, FormatoTemplateBiometrico.AnsiBinario);
            if (ansi.Exito)
            {
                return ansi;
            }

            ResultadoImportacionTemplate iso = ImportarBinario(bytes, Constants.Formats.Fmd.ISO, FormatoTemplateBiometrico.IsoBinario);
            if (iso.Exito)
            {
                return iso;
            }

            return ResultadoImportacionTemplate.Fallo(FormatoTemplateBiometrico.Desconocido, "formato-desconocido");
        }

        private static ResultadoImportacionTemplate ImportarXml(string xml)
        {
            try
            {
                Fmd fmd = Fmd.DeserializeXml(xml);
                if (fmd == null || fmd.ViewCount == 0)
                {
                    return ResultadoImportacionTemplate.Fallo(FormatoTemplateBiometrico.XmlHistorico, "xml-no-valido");
                }

                return ResultadoImportacionTemplate.Ok(fmd, FormatoTemplateBiometrico.XmlHistorico);
            }
            catch (Exception)
            {
                return ResultadoImportacionTemplate.Fallo(FormatoTemplateBiometrico.XmlHistorico, "xml-no-valido");
            }
        }

        private static ResultadoImportacionTemplate ImportarBinario(byte[] bytes, Constants.Formats.Fmd formatoOrigen, FormatoTemplateBiometrico formato)
        {
            try
            {
                DataResult<Fmd> importado = Importer.ImportFmd(bytes, formatoOrigen, Constants.Formats.Fmd.ANSI);
                if (importado.ResultCode == Constants.ResultCode.DP_SUCCESS && importado.Data != null && importado.Data.ViewCount > 0)
                {
                    return ResultadoImportacionTemplate.Ok(importado.Data, formato);
                }
            }
            catch (Exception)
            {
                return ResultadoImportacionTemplate.Fallo(formato, "no-importable");
            }

            return ResultadoImportacionTemplate.Fallo(formato, "no-importable");
        }

        private static bool PareceXml(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return false;
            }

            string limpio = texto.Trim().TrimStart('\uFEFF');
            if (limpio.Length == 0 || limpio[0] != '<')
            {
                return false;
            }

            return limpio.IndexOf("FMD", StringComparison.OrdinalIgnoreCase) >= 0
                || limpio.IndexOf("Fid", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string DecodificarTexto(byte[] bytes)
        {
            int inicio = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                inicio = 3;
            }

            return Encoding.UTF8.GetString(bytes, inicio, bytes.Length - inicio).Trim().TrimStart('\uFEFF');
        }

        private static bool TryDecodificarBase64(string valor, out byte[] bytes)
        {
            bytes = null;
            var compacto = new StringBuilder(valor.Length);
            for (int i = 0; i < valor.Length; i++)
            {
                char caracter = valor[i];
                if (caracter == ' ' || caracter == '\t' || caracter == '\r' || caracter == '\n')
                {
                    continue;
                }

                compacto.Append(caracter);
            }

            if (compacto.Length == 0 || compacto.Length % 4 != 0)
            {
                return false;
            }

            for (int i = 0; i < compacto.Length; i++)
            {
                char caracter = compacto[i];
                bool esBase64 = (caracter >= 'A' && caracter <= 'Z')
                    || (caracter >= 'a' && caracter <= 'z')
                    || (caracter >= '0' && caracter <= '9')
                    || caracter == '+'
                    || caracter == '/'
                    || caracter == '=';
                if (!esBase64)
                {
                    return false;
                }

                if (caracter == '=' && i < compacto.Length - 2)
                {
                    return false;
                }
            }

            try
            {
                bytes = Convert.FromBase64String(compacto.ToString());
                return bytes != null;
            }
            catch (FormatException)
            {
                bytes = null;
                return false;
            }
        }
    }
}
