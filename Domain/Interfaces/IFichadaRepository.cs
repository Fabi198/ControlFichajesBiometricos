using DevsFingerPrint.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevsFingerPrint.Domain.Interfaces
{
    public interface IFichadaRepository
    {

        // Operaciones locales
        void GuardarFichadaLocal(Fichada fichada);
        IEnumerable<Fichada> ObtenerFichadasPendientes();
        void MarcarComoSincronizadas(IEnumerable<int> idsFichadas);

        // Operaciones de cache local
        IEnumerable<Huella> ObtenerHuellasLocales();
        void SincronizarCatalogoEmpresa(IEnumerable<Empleado> empleados, IEnumerable<Huella> huellas);

        // Sync remoto con el backend
        bool EnviarLoteFichadas(IEnumerable<Fichada> fichadas);

    }
}
