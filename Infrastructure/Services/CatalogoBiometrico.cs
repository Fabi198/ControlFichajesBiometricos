using DevsFingerPrint.Domain.Models;
using System;
using System.Collections.Generic;

namespace DevsFingerPrint.Infrastructure.Services
{
    public sealed class ResultadoLista<T>
    {
        private ResultadoLista(bool exitoso, List<T> elementos, string motivo)
        {
            Exitoso = exitoso;
            Elementos = elementos ?? new List<T>();
            Motivo = motivo;
        }

        public bool Exitoso { get; private set; }
        public List<T> Elementos { get; private set; }
        public string Motivo { get; private set; }

        public static ResultadoLista<T> Ok(List<T> elementos)
        {
            return new ResultadoLista<T>(true, elementos, "ok");
        }

        public static ResultadoLista<T> Fallo(string motivo)
        {
            return new ResultadoLista<T>(false, new List<T>(), motivo);
        }
    }

    public sealed class ResumenCatalogoBiometrico
    {
        public int EmpleadosActivos { get; set; }
        public int HuellasRecibidas { get; set; }
        public int HuellasValidas { get; set; }
        public int HuellasIncompatibles { get; set; }
        public int AnsiBinario { get; set; }
        public int IsoBinario { get; set; }
        public int XmlHistorico { get; set; }
        public int Desconocido { get; set; }
        public List<int> IndicesIncompatibles { get; set; }
        public List<HuellaEnMemoria> Compatibles { get; set; }
        public DateTime? UltimaSincronizacionCorrecta { get; set; }

        public string TextoLog()
        {
            return "empleados=" + EmpleadosActivos
                + " recibidas=" + HuellasRecibidas
                + " validas=" + HuellasValidas
                + " incompatibles=" + HuellasIncompatibles
                + " ansi=" + AnsiBinario
                + " iso=" + IsoBinario
                + " xml=" + XmlHistorico
                + " desconocidas=" + Desconocido
                + " en-memoria=" + HuellasValidas
                + " indices=" + TextoIndices();
        }

        private string TextoIndices()
        {
            if (IndicesIncompatibles == null || IndicesIncompatibles.Count == 0)
            {
                return "-";
            }

            int limite = Math.Min(30, IndicesIncompatibles.Count);
            var texto = new System.Text.StringBuilder();
            for (int i = 0; i < limite; i++)
            {
                if (i > 0)
                {
                    texto.Append(',');
                }

                texto.Append(IndicesIncompatibles[i]);
            }

            if (IndicesIncompatibles.Count > limite)
            {
                texto.Append(",...");
            }

            return texto.ToString();
        }
    }

    public static class CatalogoBiometrico
    {
        public static bool EmpresasHomogeneas(IList<Empleado> empleados)
        {
            if (empleados == null)
            {
                return false;
            }

            int empresaId = 0;
            for (int i = 0; i < empleados.Count; i++)
            {
                Empleado empleado = empleados[i];
                if (empleado == null || empleado.Id <= 0 || empleado.EmpresaId <= 0)
                {
                    return false;
                }

                if (empresaId == 0)
                {
                    empresaId = empleado.EmpresaId;
                }
                else if (empleado.EmpresaId != empresaId)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool PrepararReemplazo(
            IList<Empleado> empleados,
            IList<Huella> huellas,
            int sucursalId,
            out List<Empleado> empleadosDestino,
            out List<Huella> huellasDestino,
            out string motivo)
        {
            empleadosDestino = new List<Empleado>();
            huellasDestino = new List<Huella>();
            if (empleados == null || huellas == null)
            {
                motivo = "catalogo-nulo";
                return false;
            }

            if (!EmpresasHomogeneas(empleados))
            {
                motivo = "catalogo-mixto";
                return false;
            }

            if (empleados.Count == 0 && huellas.Count > 0)
            {
                motivo = "catalogo-huerfano";
                return false;
            }

            if (!FiltrarSucursal(empleados, sucursalId, empleadosDestino))
            {
                motivo = "catalogo-sucursal";
                return false;
            }

            var ids = new HashSet<int>();
            for (int i = 0; i < empleadosDestino.Count; i++)
            {
                ids.Add(empleadosDestino[i].Id);
            }

            for (int i = 0; i < huellas.Count; i++)
            {
                Huella huella = huellas[i];
                if (huella != null && ids.Contains(huella.EmpleadoId))
                {
                    huellasDestino.Add(huella);
                }
            }

            motivo = "ok";
            return true;
        }

        private static bool FiltrarSucursal(IList<Empleado> empleados, int sucursalId, List<Empleado> destino)
        {
            if (sucursalId <= 0 || empleados.Count == 0)
            {
                destino.AddRange(empleados);
                return true;
            }

            bool algunoConSucursal = false;
            bool algunoCoincide = false;
            for (int i = 0; i < empleados.Count; i++)
            {
                Empleado empleado = empleados[i];
                if (empleado.SucursalId.HasValue && empleado.SucursalId.Value > 0)
                {
                    algunoConSucursal = true;
                    if (empleado.SucursalId.Value == sucursalId)
                    {
                        algunoCoincide = true;
                        destino.Add(empleado);
                    }
                }
                else
                {
                    destino.Add(empleado);
                }
            }

            if (algunoConSucursal && !algunoCoincide)
            {
                destino.Clear();
                return false;
            }

            return true;
        }

        public static string ReferenciaInvalida(Huella huella, ISet<int> empleadosActivos)
        {
            if (huella == null)
            {
                return "nula";
            }

            if (huella.EmpleadoId <= 0 || empleadosActivos == null || !empleadosActivos.Contains(huella.EmpleadoId))
            {
                return "empleado";
            }

            if (huella.IndiceDedo < 0 || huella.IndiceDedo > 10)
            {
                return "dedo";
            }

            if (string.IsNullOrWhiteSpace(huella.TemplateBiometrico))
            {
                return "vacia";
            }

            return null;
        }

        public static ResumenCatalogoBiometrico Construir(IEnumerable<Empleado> empleados, IEnumerable<Huella> huellas)
        {
            var resumen = new ResumenCatalogoBiometrico
            {
                Compatibles = new List<HuellaEnMemoria>(),
                IndicesIncompatibles = new List<int>()
            };
            var activos = new HashSet<int>();
            if (empleados != null)
            {
                foreach (Empleado empleado in empleados)
                {
                    if (empleado != null && empleado.Id > 0 && empleado.Activo)
                    {
                        activos.Add(empleado.Id);
                    }
                }
            }

            resumen.EmpleadosActivos = activos.Count;
            if (huellas == null)
            {
                return resumen;
            }

            int indice = 0;
            foreach (Huella huella in huellas)
            {
                resumen.HuellasRecibidas++;
                if (ReferenciaInvalida(huella, activos) != null)
                {
                    resumen.HuellasIncompatibles++;
                    resumen.Desconocido++;
                    resumen.IndicesIncompatibles.Add(indice);
                    indice++;
                    continue;
                }

                ResultadoImportacionTemplate importada = TemplateBiometricoImporter.TryImportarTemplateBiometrico(huella.TemplateBiometrico);
                ContarFormato(resumen, importada.Formato);
                if (!importada.Exito || importada.Fmd == null)
                {
                    resumen.HuellasIncompatibles++;
                    resumen.IndicesIncompatibles.Add(indice);
                    indice++;
                    continue;
                }

                resumen.HuellasValidas++;
                resumen.Compatibles.Add(new HuellaEnMemoria
                {
                    EmpleadoId = huella.EmpleadoId,
                    IndiceDedo = huella.IndiceDedo,
                    Plantilla = importada.Fmd
                });
                indice++;
            }

            return resumen;
        }

        public static string TextoEstado(
            bool lectorConectado,
            string serial,
            ResumenCatalogoBiometrico resumen,
            int fichadasPendientes,
            string estadoApi)
        {
            ResumenCatalogoBiometrico datos = resumen ?? new ResumenCatalogoBiometrico();
            string sincronizacion = datos.UltimaSincronizacionCorrecta.HasValue
                ? datos.UltimaSincronizacionCorrecta.Value.ToString("dd/MM/yyyy HH:mm")
                : "Sin sincronizar";
            return "Lector: " + (lectorConectado ? "Conectado" : "Desconectado")
                + Environment.NewLine + "Serial: " + (string.IsNullOrWhiteSpace(serial) ? "No disponible" : serial)
                + Environment.NewLine + "Empleados sincronizados: " + datos.EmpleadosActivos
                + Environment.NewLine + "Huellas recibidas: " + datos.HuellasRecibidas
                + Environment.NewLine + "Huellas disponibles: " + datos.HuellasValidas
                + Environment.NewLine + "Huellas incompatibles: " + datos.HuellasIncompatibles
                + Environment.NewLine + "Última sincronización: " + sincronizacion
                + Environment.NewLine + "Fichadas pendientes: " + fichadasPendientes
                + Environment.NewLine + "API: " + (string.IsNullOrWhiteSpace(estadoApi) ? "Sin datos" : estadoApi);
        }

        private static void ContarFormato(ResumenCatalogoBiometrico resumen, FormatoTemplateBiometrico formato)
        {
            if (formato == FormatoTemplateBiometrico.AnsiBinario)
            {
                resumen.AnsiBinario++;
            }
            else if (formato == FormatoTemplateBiometrico.IsoBinario)
            {
                resumen.IsoBinario++;
            }
            else if (formato == FormatoTemplateBiometrico.XmlHistorico)
            {
                resumen.XmlHistorico++;
            }
            else
            {
                resumen.Desconocido++;
            }
        }
    }
}
