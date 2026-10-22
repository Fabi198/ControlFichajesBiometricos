using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data.SQLite;
using DevsFingerPrint.Domain.Models;
using DevsFingerPrint.Infrastructure.Data;
using DevsFingerPrint.Infrastructure.Repositories;
using Xunit;

namespace DevsFingerPrint.Tests
{
    public sealed class FichadaRepositoryTests : IDisposable
    {
        private readonly string _databasePath;
        private readonly FichadaRepository _repository;

        public FichadaRepositoryTests()
        {
            string testDirectory = Path.Combine(Path.GetTempPath(), "DevsFingerPrint.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            _databasePath = Path.Combine(testDirectory, "test.db");
            LocalDatabase.Inicializar(_databasePath);
            _repository = new FichadaRepository(LocalDatabase.CrearConnectionString(_databasePath, pooling: false));
        }

        [Fact]
        public void GuardarFichadaLocal_PersisteOrdenCronologicoPendiente()
        {
            var tarde = CrearFichada(1, new DateTime(2026, 10, 12, 18, 0, 0));
            var manana = CrearFichada(1, new DateTime(2026, 10, 12, 9, 0, 0));
            _repository.GuardarFichadaLocal(tarde);
            _repository.GuardarFichadaLocal(manana);

            var pendientes = _repository.ObtenerFichadasPendientes().ToList();

            Assert.Equal(new[] { manana.FechaHora, tarde.FechaHora }, pendientes.Select(item => item.FechaHora));
            Assert.All(pendientes, item => Assert.False(item.Sincronizado));
            Assert.All(pendientes, item => Assert.Equal("Biometrico", item.Metodo));
        }

        [Fact]
        public void MarcarComoSincronizadas_NoIncluyeFichadasNoConfirmadas()
        {
            var confirmada = CrearFichada(1, new DateTime(2026, 10, 12, 9, 0, 0));
            var noConfirmada = CrearFichada(2, new DateTime(2026, 10, 12, 10, 0, 0));
            _repository.GuardarFichadaLocal(confirmada);
            _repository.GuardarFichadaLocal(noConfirmada);
            var ids = _repository.ObtenerFichadasPendientes().ToList();

            _repository.MarcarComoSincronizadas(new[] { ids[0].Id });

            var pendientes = _repository.ObtenerFichadasPendientes().ToList();
            Assert.Single(pendientes);
            Assert.Equal(ids[1].Id, pendientes[0].Id);
        }

        [Fact]
        public void MarcarComoSincronizadas_AceptaListaVaciaSinCambiarCola()
        {
            _repository.GuardarFichadaLocal(CrearFichada(1, new DateTime(2026, 10, 12, 9, 0, 0)));

            _repository.MarcarComoSincronizadas(Array.Empty<int>());

            Assert.Single(_repository.ObtenerFichadasPendientes());
        }

        [Fact]
        public void SincronizarCatalogoEmpresa_FalloRevierteBorradoYActualizacionParcial()
        {
            var original = CrearEmpleado(1, "LEG-ORIGINAL");
            _repository.SincronizarCatalogoEmpresa(new[] { original }, Array.Empty<Huella>());
            var repetido = CrearEmpleado(2, "LEG-NUEVO");

            Assert.ThrowsAny<Exception>(() => _repository.SincronizarCatalogoEmpresa(
                new[] { repetido, repetido },
                Array.Empty<Huella>()));

            Assert.Equal(original.DNI, _repository.ObtenerDniPorEmpleadoId(1));
            Assert.Null(_repository.ObtenerDniPorEmpleadoId(2));
        }

        [Fact]
        public void ObtenerHorarioLaboral_LeeHorarioYNoInventaHorarioAusente()
        {
            var empleado = CrearEmpleado(1, "LEG-HORARIO");
            empleado.Horario = "09:00 a 18:30";
            _repository.SincronizarCatalogoEmpresa(new[] { empleado }, Array.Empty<Huella>());

            var horario = _repository.ObtenerHorarioLaboral(1);

            Assert.NotNull(horario);
            Assert.Equal(TimeSpan.FromHours(9), horario.HoraEntrada);
            Assert.Equal(new TimeSpan(18, 30, 0), horario.HoraSalida);
            Assert.Null(_repository.ObtenerHorarioLaboral(99));
        }

        public void Dispose()
        {
            SQLiteConnection.ClearAllPools();
            string directory = Path.GetDirectoryName(_databasePath);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static Fichada CrearFichada(int empleadoId, DateTime fechaHora)
        {
            return new Fichada
            {
                EmpleadoId = empleadoId,
                FechaHora = fechaHora,
                TipoRegistro = "Entrada",
                Metodo = "Biometrico",
                Sincronizado = false
            };
        }

        private static Empleado CrearEmpleado(int id, string legajo)
        {
            return new Empleado
            {
                Id = id,
                EmpresaId = 1,
                Legajo = legajo,
                DNI = $"TEST-{id}",
                Nombre = "Ada",
                Apellido = "Lovelace",
                Activo = true
            };
        }
    }
}
