using DPUruNet;

namespace DevsFingerPrint.Domain.Models
{
    public sealed class HuellaEnMemoria
    {
        public int EmpleadoId { get; set; }
        public int IndiceDedo { get; set; }
        public Fmd Plantilla { get; set; }
    }
}
