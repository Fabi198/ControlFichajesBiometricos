using DevsFingerPrint.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DPUruNet;

namespace DevsFingerPrint.Domain.Interfaces
{
    public interface IBiometricService
    {

        //Eventos
        event Action<Fmd> OnHuellaCapturada;
        event Action<int> OnEmpleadoIdentificado;
        event Action OnHuellaNoReconocida;
        event Action<string> OnEstadoCambiado;
        event Action<int> OnProgresoEnrolamiento;
        DPUruNet.Reader ObtenerLectorActual();

        // Control del lector USB
        bool IniciarLectura();
        void DetenerLectura();

        //Enrolamiento
        void IniciarEnrolamiento();
        void CancelarEnrolamiento();
        string ObtenerTemplateBase64Resultado();

        //Verificacion
        bool VerificarHuella(Fmd huellaCapturada, string templateBase64);
        int? IdentificarEmpleado(Fmd huellaCapturada, IEnumerable<Huella> huellasCargadas);

    }
}
