using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Incidencias;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria del servicio de gestión de incidencias operativas (UC-11).
/// </summary>
public class MockIncidenciasApi : IIncidenciasApi
{
    private static readonly List<IncidenciaDto> Incidencias = new()
    {
        new IncidenciaDto
        {
            Id = Guid.Parse("bbbbbbbb-0001-0000-0000-000000000001"),
            NumeroIncidencia = "INC-2026-0038",
            FechaRegistro = DateTime.UtcNow.AddHours(-5),
            Tipo = "AveriaVehiculo",
            Severidad = "Alta",
            Estado = "Abierta",
            Descripcion = "Pinchadura múltiple y daño en tren delantero del utilitario en Av. Italia esq. Propios. Vehículo detenido.",
            VehiculoMatricula = "STX 1029",
            RepartidorNombre = "Lucía Fernández",
            HojaDeRutaId = Guid.Parse("aaaaaaaa-0001-0000-0000-000000000001"),
            NumeroRuta = "HR-2026-0101",
            DespachadorRegistro = "Felipe Bernardi",
            HistorialSeguimiento = new List<EntradaBitacoraIncidenciaDto>
            {
                new()
                {
                    FechaHora = DateTime.UtcNow.AddHours(-5),
                    Usuario = "Felipe Bernardi",
                    Comentario = "Incidencia reportada por el repartidor vía llamada de emergencia. Vehículo inmovilizado.",
                    NuevoEstado = "Abierta"
                }
            }
        },
        new IncidenciaDto
        {
            Id = Guid.Parse("bbbbbbbb-0002-0000-0000-000000000002"),
            NumeroIncidencia = "INC-2026-0039",
            FechaRegistro = DateTime.UtcNow.AddHours(-3),
            Tipo = "PaqueteDanado",
            Severidad = "Media",
            Estado = "EnInvestigacion",
            Descripcion = "Bulto exterior con abolladura y cinta rasgada detectado durante el traslado. Requiere verificación de contenido.",
            EnvioAfectadoId = Guid.Parse("11111111-0002-0000-0000-000000000002"),
            CodigoSeguimientoEnvio = "TRK-2026-0002",
            DestinatarioEnvio = "Carlos Méndez",
            HojaDeRutaId = Guid.Parse("aaaaaaaa-0001-0000-0000-000000000001"),
            NumeroRuta = "HR-2026-0101",
            RepartidorNombre = "Lucía Fernández",
            VehiculoMatricula = "STX 1029",
            DespachadorRegistro = "Felipe Bernardi",
            HistorialSeguimiento = new List<EntradaBitacoraIncidenciaDto>
            {
                new()
                {
                    FechaHora = DateTime.UtcNow.AddHours(-3),
                    Usuario = "Felipe Bernardi",
                    Comentario = "Reporte ingresado tras aviso fotográfico del repartidor.",
                    NuevoEstado = "Abierta"
                },
                new()
                {
                    FechaHora = DateTime.UtcNow.AddHours(-1).AddMinutes(-30),
                    Usuario = "Felipe Bernardi",
                    Comentario = "Se contactó al comercio emisor para evaluar si corresponde entrega con salvedad o retorno para reempaque.",
                    EstadoAnterior = "Abierta",
                    NuevoEstado = "EnInvestigacion"
                }
            }
        },
        new IncidenciaDto
        {
            Id = Guid.Parse("bbbbbbbb-0003-0000-0000-000000000003"),
            NumeroIncidencia = "INC-2026-0037",
            FechaRegistro = DateTime.UtcNow.AddDays(-1).AddHours(2),
            FechaResolucion = DateTime.UtcNow.AddHours(-8),
            Tipo = "AusenciaRepartidor",
            Severidad = "Critica",
            Estado = "Cerrada",
            Descripcion = "Repartidor titular no se presentó al turno matutino por certificado médico imprevisto.",
            HojaDeRutaId = Guid.Parse("aaaaaaaa-0002-0000-0000-000000000002"),
            NumeroRuta = "HR-2026-0100",
            RepartidorNombre = "Martín Silva",
            VehiculoMatricula = "SAA 3042",
            DespachadorRegistro = "Felipe Bernardi",
            DespachadorCierre = "Felipe Bernardi",
            ResolucionDetalle = "Se reasignó la ruta al repartidor de reserva y se notificó a los destinatarios de los nuevos horarios estimados.",
            HistorialSeguimiento = new List<EntradaBitacoraIncidenciaDto>
            {
                new()
                {
                    FechaHora = DateTime.UtcNow.AddDays(-1).AddHours(2),
                    Usuario = "Felipe Bernardi",
                    Comentario = "Apertura por incomparecencia al control de presentismo de depósito.",
                    NuevoEstado = "Abierta"
                },
                new()
                {
                    FechaHora = DateTime.UtcNow.AddDays(-1).AddHours(3),
                    Usuario = "Felipe Bernardi",
                    Comentario = "Convocado chofer sustituto. Realizando traspaso de llaves y documentación.",
                    EstadoAnterior = "Abierta",
                    NuevoEstado = "EnInvestigacion"
                },
                new()
                {
                    FechaHora = DateTime.UtcNow.AddHours(-8),
                    Usuario = "Felipe Bernardi",
                    Comentario = "Ruta completada y rendida por el chofer sustituto sin reclamos adicionales.",
                    EstadoAnterior = "EnInvestigacion",
                    NuevoEstado = "Cerrada"
                }
            }
        },
        new IncidenciaDto
        {
            Id = Guid.Parse("bbbbbbbb-0004-0000-0000-000000000004"),
            NumeroIncidencia = "INC-2026-0036",
            FechaRegistro = DateTime.UtcNow.AddDays(-2),
            FechaResolucion = DateTime.UtcNow.AddDays(-1),
            Tipo = "ReclamoDestinatario",
            Severidad = "Baja",
            Estado = "Cerrada",
            Descripcion = "Cliente manifiesta que el timbre no funcionaba y solicita aviso telefónico previo a la próxima visita.",
            EnvioAfectadoId = Guid.Parse("11111111-0006-0000-0000-000000000006"),
            CodigoSeguimientoEnvio = "TRK-2026-0006",
            DestinatarioEnvio = "Mariana Castro",
            DespachadorRegistro = "Felipe Bernardi",
            DespachadorCierre = "Felipe Bernardi",
            ResolucionDetalle = "Se registró la aclaración en la ficha del envío y se pactó entrega para la franja tarde.",
            HistorialSeguimiento = new List<EntradaBitacoraIncidenciaDto>
            {
                new()
                {
                    FechaHora = DateTime.UtcNow.AddDays(-2),
                    Usuario = "Felipe Bernardi",
                    Comentario = "Reclamo ingresado mediante canal de atención.",
                    NuevoEstado = "Abierta"
                },
                new()
                {
                    FechaHora = DateTime.UtcNow.AddDays(-1),
                    Usuario = "Felipe Bernardi",
                    Comentario = "Coordinación completada satisfactoriamente con el destinatario.",
                    EstadoAnterior = "Abierta",
                    NuevoEstado = "Cerrada"
                }
            }
        }
    };

    private static int ContadorSecuencia = 40;

    public Task<IReadOnlyList<IncidenciaDto>> ListarIncidenciasAsync(FiltroIncidenciasDto? filtro = null, CancellationToken cancelacion = default)
    {
        var query = Incidencias.AsEnumerable();

        if (filtro != null)
        {
            if (!string.IsNullOrEmpty(filtro.Estado) && filtro.Estado != "Todos")
            {
                query = query.Where(i => string.Equals(i.Estado, filtro.Estado, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(filtro.Tipo) && filtro.Tipo != "Todos")
            {
                query = query.Where(i => string.Equals(i.Tipo, filtro.Tipo, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(filtro.Severidad) && filtro.Severidad != "Todos")
            {
                query = query.Where(i => string.Equals(i.Severidad, filtro.Severidad, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
            {
                var termino = filtro.Busqueda.Trim().ToLowerInvariant();
                query = query.Where(i =>
                    i.NumeroIncidencia.ToLowerInvariant().Contains(termino) ||
                    i.Descripcion.ToLowerInvariant().Contains(termino) ||
                    (i.CodigoSeguimientoEnvio != null && i.CodigoSeguimientoEnvio.ToLowerInvariant().Contains(termino)) ||
                    (i.DestinatarioEnvio != null && i.DestinatarioEnvio.ToLowerInvariant().Contains(termino)) ||
                    (i.NumeroRuta != null && i.NumeroRuta.ToLowerInvariant().Contains(termino)) ||
                    (i.RepartidorNombre != null && i.RepartidorNombre.ToLowerInvariant().Contains(termino))
                );
            }
        }

        var lista = query.OrderByDescending(i => i.FechaRegistro).ToList();
        return Task.FromResult<IReadOnlyList<IncidenciaDto>>(lista);
    }

    public Task<IncidenciaDto?> ObtenerIncidenciaPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var item = Incidencias.FirstOrDefault(i => i.Id == id);
        return Task.FromResult(item);
    }

    public Task<IncidenciaDto> RegistrarIncidenciaAsync(RegistrarIncidenciaDto dto, string despachadorUsuario, CancellationToken cancelacion = default)
    {
        ContadorSecuencia++;
        var numero = $"INC-2026-{ContadorSecuencia:D4}";

        var nueva = new IncidenciaDto
        {
            Id = Guid.NewGuid(),
            NumeroIncidencia = numero,
            FechaRegistro = DateTime.UtcNow,
            Tipo = dto.Tipo,
            Severidad = dto.Severidad,
            Estado = "Abierta",
            Descripcion = dto.Descripcion.Trim(),
            EnvioAfectadoId = dto.EnvioAfectadoId,
            CodigoSeguimientoEnvio = dto.CodigoSeguimientoEnvio,
            HojaDeRutaId = dto.HojaDeRutaId,
            RepartidorNombre = dto.RepartidorNombre,
            VehiculoMatricula = dto.VehiculoMatricula,
            DespachadorRegistro = despachadorUsuario,
            HistorialSeguimiento = new List<EntradaBitacoraIncidenciaDto>
            {
                new()
                {
                    FechaHora = DateTime.UtcNow,
                    Usuario = despachadorUsuario,
                    Comentario = string.IsNullOrWhiteSpace(dto.AccionInmediata)
                        ? "Incidencia registrada formalmente en el sistema."
                        : $"Registro inicial. Acción inmediata: {dto.AccionInmediata.Trim()}",
                    NuevoEstado = "Abierta"
                }
            }
        };

        Incidencias.Insert(0, nueva);
        return Task.FromResult(nueva);
    }

    public Task<bool> AgregarSeguimientoAsync(AgregarSeguimientoIncidenciaDto dto, string despachadorUsuario, CancellationToken cancelacion = default)
    {
        var indice = Incidencias.FindIndex(i => i.Id == dto.IncidenciaId);
        if (indice < 0) return Task.FromResult(false);

        var actual = Incidencias[indice];
        var nuevoEstado = string.IsNullOrWhiteSpace(dto.NuevoEstado) ? actual.Estado : dto.NuevoEstado;

        var nuevaEntrada = new EntradaBitacoraIncidenciaDto
        {
            FechaHora = DateTime.UtcNow,
            Usuario = despachadorUsuario,
            Comentario = dto.Comentario.Trim(),
            EstadoAnterior = actual.Estado,
            NuevoEstado = nuevoEstado
        };

        var historialActualizado = new List<EntradaBitacoraIncidenciaDto>(actual.HistorialSeguimiento)
        {
            nuevaEntrada
        };

        var actualizada = actual with
        {
            Estado = nuevoEstado,
            HistorialSeguimiento = historialActualizado
        };

        Incidencias[indice] = actualizada;
        return Task.FromResult(true);
    }

    public Task<bool> CerrarIncidenciaAsync(CerrarIncidenciaDto dto, string despachadorUsuario, CancellationToken cancelacion = default)
    {
        var indice = Incidencias.FindIndex(i => i.Id == dto.IncidenciaId);
        if (indice < 0) return Task.FromResult(false);

        var actual = Incidencias[indice];

        var nuevaEntrada = new EntradaBitacoraIncidenciaDto
        {
            FechaHora = DateTime.UtcNow,
            Usuario = despachadorUsuario,
            Comentario = $"Incidencia cerrada. Resolución: {dto.ResolucionDetalle.Trim()}",
            EstadoAnterior = actual.Estado,
            NuevoEstado = "Cerrada"
        };

        var historialActualizado = new List<EntradaBitacoraIncidenciaDto>(actual.HistorialSeguimiento)
        {
            nuevaEntrada
        };

        var cerrada = actual with
        {
            Estado = "Cerrada",
            FechaResolucion = DateTime.UtcNow,
            DespachadorCierre = despachadorUsuario,
            ResolucionDetalle = dto.ResolucionDetalle.Trim(),
            HistorialSeguimiento = historialActualizado
        };

        Incidencias[indice] = cerrada;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<EnvioDisponibleIncidenciaDto>> ListarEnviosDisponiblesAsync(CancellationToken cancelacion = default)
    {
        var envios = new List<EnvioDisponibleIncidenciaDto>
        {
            new(Guid.Parse("11111111-0001-0000-0000-000000000001"), "TRK-2026-0001", "Lucía Fernández", "Av. 18 de Julio 1420", "Entregado"),
            new(Guid.Parse("11111111-0002-0000-0000-000000000002"), "TRK-2026-0002", "Carlos Méndez", "Bvar. Artigas 2341", "EnTransito"),
            new(Guid.Parse("11111111-0006-0000-0000-000000000006"), "TRK-2026-0006", "Mariana Castro", "21 de Setiembre 2890", "NoEntregado"),
            new(Guid.Parse("11111111-0920-0000-0000-000000000001"), "TRK-2026-0920", "Ana Laura García", "Av. Brasil 2980", "AsignadoARuta"),
            new(Guid.Parse("11111111-0921-0000-0000-000000000002"), "TRK-2026-0921", "Esteban Morales", "Ellauri 350", "EnDeposito"),
            new(Guid.Parse("11111111-0922-0000-0000-000000000003"), "TRK-2026-0922", "Sofía Recoba", "Luis A. de Herrera 1243", "EnDeposito")
        };
        return Task.FromResult<IReadOnlyList<EnvioDisponibleIncidenciaDto>>(envios);
    }

    public Task<IReadOnlyList<RutaDisponibleIncidenciaDto>> ListarRutasDisponiblesAsync(CancellationToken cancelacion = default)
    {
        var rutas = new List<RutaDisponibleIncidenciaDto>
        {
            new(Guid.Parse("aaaaaaaa-0001-0000-0000-000000000001"), "HR-2026-0101", "Lucía Fernández", "STX-1029", "Liberada"),
            new(Guid.Parse("aaaaaaaa-0002-0000-0000-000000000002"), "HR-2026-0100", "Martín Silva", "UR-3042", "Liberada"),
            new(Guid.Parse("aaaaaaaa-0003-0000-0000-000000000003"), "HR-2026-0099", "Roberto Gómez", "MAT-5541", "Despachada")
        };
        return Task.FromResult<IReadOnlyList<RutaDisponibleIncidenciaDto>>(rutas);
    }
}
