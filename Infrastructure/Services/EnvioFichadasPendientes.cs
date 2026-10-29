using DevsFingerPrint.Domain.Models;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DevsFingerPrint.Infrastructure.Services
{
    public enum EstadoEnvioFichadas
    {
        SinPendientes,
        Confirmado,
        FalloTemporal,
        Fallo,
        Ocupado
    }

    public sealed class ResultadoEnvioFichadas
    {
        public EstadoEnvioFichadas Estado { get; private set; }
        public int Cantidad { get; private set; }
        public bool ConsultoServidor { get; private set; }

        public static ResultadoEnvioFichadas Crear(EstadoEnvioFichadas estado, int cantidad, bool consultoServidor)
        {
            return new ResultadoEnvioFichadas
            {
                Estado = estado,
                Cantidad = cantidad,
                ConsultoServidor = consultoServidor
            };
        }
    }

    public sealed class EnvioFichadasPendientes
    {
        private readonly OperacionExclusiva _puerta = new OperacionExclusiva();
        private readonly Func<IEnumerable<Fichada>> _leerPendientes;
        private readonly Func<IReadOnlyList<Fichada>, EstadoEnvioFichadas> _enviarLote;
        private readonly Action<IReadOnlyList<int>> _marcarConfirmadas;
        private int _trabajoDiferido;

        public EnvioFichadasPendientes(
            Func<IEnumerable<Fichada>> leerPendientes,
            Func<IReadOnlyList<Fichada>, EstadoEnvioFichadas> enviarLote,
            Action<IReadOnlyList<int>> marcarConfirmadas)
        {
            if (leerPendientes == null)
            {
                throw new ArgumentNullException(nameof(leerPendientes));
            }

            if (enviarLote == null)
            {
                throw new ArgumentNullException(nameof(enviarLote));
            }

            if (marcarConfirmadas == null)
            {
                throw new ArgumentNullException(nameof(marcarConfirmadas));
            }

            _leerPendientes = leerPendientes;
            _enviarLote = enviarLote;
            _marcarConfirmadas = marcarConfirmadas;
        }

        public ResultadoEnvioFichadas Ejecutar()
        {
            if (!_puerta.TryEnter())
            {
                Interlocked.Exchange(ref _trabajoDiferido, 1);
                return ResultadoEnvioFichadas.Crear(EstadoEnvioFichadas.Ocupado, 0, false);
            }

            ResultadoEnvioFichadas resultado;
            try
            {
                resultado = Drenar();
            }
            finally
            {
                _puerta.Salir();
            }

            if (resultado.Estado != EstadoEnvioFichadas.FalloTemporal
                && resultado.Estado != EstadoEnvioFichadas.Fallo
                && Interlocked.Exchange(ref _trabajoDiferido, 0) == 1)
            {
                ResultadoEnvioFichadas siguiente = Ejecutar();
                if (siguiente.Estado != EstadoEnvioFichadas.Ocupado)
                {
                    return siguiente;
                }
            }

            return resultado;
        }

        private ResultadoEnvioFichadas Drenar()
        {
            ResultadoEnvioFichadas ultimo = ResultadoEnvioFichadas.Crear(EstadoEnvioFichadas.SinPendientes, 0, false);
            int confirmadas = 0;
            bool consulto = false;
            while (true)
            {
                Interlocked.Exchange(ref _trabajoDiferido, 0);
                ultimo = ProcesarUnLote();
                consulto = consulto || ultimo.ConsultoServidor;
                if (ultimo.Estado == EstadoEnvioFichadas.Confirmado)
                {
                    confirmadas += ultimo.Cantidad;
                }

                if (ultimo.Estado == EstadoEnvioFichadas.FalloTemporal || ultimo.Estado == EstadoEnvioFichadas.Fallo)
                {
                    return ultimo;
                }

                if (Volatile.Read(ref _trabajoDiferido) == 0)
                {
                    if (confirmadas > 0)
                    {
                        return ResultadoEnvioFichadas.Crear(EstadoEnvioFichadas.Confirmado, confirmadas, consulto);
                    }

                    return ultimo;
                }
            }
        }

        private ResultadoEnvioFichadas ProcesarUnLote()
        {
            IEnumerable<Fichada> origen = _leerPendientes() ?? new Fichada[0];
            List<Fichada> lote = TomarLote(origen);
            if (lote.Count == 0)
            {
                return ResultadoEnvioFichadas.Crear(EstadoEnvioFichadas.SinPendientes, 0, false);
            }

            EstadoEnvioFichadas estado = _enviarLote(lote);
            if (estado == EstadoEnvioFichadas.Confirmado)
            {
                var ids = new List<int>(lote.Count);
                for (int i = 0; i < lote.Count; i++)
                {
                    ids.Add(lote[i].Id);
                }

                _marcarConfirmadas(ids);
            }

            if (estado != EstadoEnvioFichadas.Confirmado
                && estado != EstadoEnvioFichadas.FalloTemporal
                && estado != EstadoEnvioFichadas.Fallo)
            {
                estado = EstadoEnvioFichadas.Fallo;
            }

            return ResultadoEnvioFichadas.Crear(estado, lote.Count, true);
        }

        public static List<Fichada> TomarLote(IEnumerable<Fichada> pendientes)
        {
            var lote = new List<Fichada>(Math.Min(CicloVidaAgente.TamanoLoteFichadas, 16));
            if (pendientes == null)
            {
                return lote;
            }

            foreach (Fichada fichada in pendientes)
            {
                if (fichada == null)
                {
                    continue;
                }

                lote.Add(fichada);
                if (lote.Count == CicloVidaAgente.TamanoLoteFichadas)
                {
                    break;
                }
            }

            return lote;
        }
    }
}
