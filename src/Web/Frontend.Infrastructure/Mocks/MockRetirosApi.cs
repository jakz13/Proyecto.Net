using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Retiros;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>Retiros en memoria para el modo demo sin backend. Respeta el mismo ciclo de estados que el servidor.</summary>
public class MockRetirosApi : IRetirosApi
{
    private readonly object _bloqueo = new();
    private readonly List<RetiroDetalleDto> _retiros = new();

    public MockRetirosApi()
    {
        Agregar("RET-DEMO000001", "Solicitado", "Lucía Fernández", "Bulevar Artigas 1500, Montevideo", "Cambio de talle", 2);
        Agregar("RET-DEMO000002", "AsignadoARuta", "Martín Sosa", "18 de Julio 1234, Montevideo", "Producto defectuoso", 1);
        Agregar("RET-DEMO000003", "EnDeposito", "Paula Techera", "Av. Italia 3456, Montevideo", "Devolución", 3);
    }

    private RetiroDetalleDto Agregar(string codigo, string estado, string contacto, string direccion, string motivo, int bultos)
    {
        var id = Guid.NewGuid();
        var resumen = new RetiroResumenDto
        {
            Id = id, CodigoSeguimiento = codigo, Estado = estado, NombreContacto = contacto, DireccionRetiro = direccion, Motivo = motivo,
            CantidadBultos = bultos, PesoTotal = bultos * 1.5m, Tarifa = 120m, FechaSolicitud = DateTime.UtcNow
        };
        var detalle = new RetiroDetalleDto
        {
            Resumen = resumen,
            Bultos = Enumerable.Range(1, bultos).Select(i => new BultoRetiroDto { Identificacion = $"{codigo}-B{i:00}", Descripcion = $"Bulto {i}", Peso = 1.5m }).ToList(),
            Eventos = new List<EventoRetiroDto> { new() { Fecha = DateTime.UtcNow, Tipo = "SolicitudRetiro", EstadoNuevo = "Solicitado", Origen = "PortalComercio" } }
        };
        lock (_bloqueo) _retiros.Add(detalle);
        return detalle;
    }

    private RetiroDetalleDto Cambiar(Guid id, string desde, string hacia, string evento)
    {
        lock (_bloqueo)
        {
            var indice = _retiros.FindIndex(r => r.Resumen.Id == id);
            if (indice < 0)
                throw new ApiExcepcion(System.Net.HttpStatusCode.NotFound, "Retiro no encontrado.");

            var actual = _retiros[indice];
            if (actual.Resumen.Estado != desde)
                throw new ApiExcepcion($"El retiro está en estado '{actual.Resumen.Estado}'; se requiere '{desde}'.");

            var nuevo = actual with
            {
                Resumen = actual.Resumen with { Estado = hacia },
                Eventos = actual.Eventos.Append(new EventoRetiroDto { Fecha = DateTime.UtcNow, Tipo = evento, EstadoAnterior = desde, EstadoNuevo = hacia, Origen = "Backoffice" }).ToList()
            };
            _retiros[indice] = nuevo;
            return nuevo;
        }
    }

    public Task<ResultadoSolicitudRetiroDto> SolicitarAsync(SolicitarRetiroDto solicitud, CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(solicitud.NombreContacto) || string.IsNullOrWhiteSpace(solicitud.Motivo) || solicitud.Bultos.Count == 0)
            throw new ApiExcepcion("Indique el contacto, el motivo y al menos un bulto.");

        var codigo = "RET-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var detalle = Agregar(codigo, "Solicitado", solicitud.NombreContacto, $"{solicitud.Calle} {solicitud.Numero}, {solicitud.Ciudad}", solicitud.Motivo, solicitud.Bultos.Count);
        return Task.FromResult(new ResultadoSolicitudRetiroDto { Id = detalle.Resumen.Id, CodigoSeguimiento = codigo, Estado = "Solicitado", Tarifa = 120m });
    }

    public Task<PaginaRetirosDto> ListarDelComercioAsync(string? estado = null, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var items = _retiros.Select(r => r.Resumen).Where(r => estado is null || r.Estado == estado).OrderByDescending(r => r.FechaSolicitud).ToList();
            return Task.FromResult(new PaginaRetirosDto { Items = items, Total = items.Count });
        }
    }

    public Task<RetiroDetalleDto?> ObtenerDelComercioAsync(Guid id, CancellationToken cancelacion = default)
    {
        lock (_bloqueo) return Task.FromResult(_retiros.FirstOrDefault(r => r.Resumen.Id == id));
    }

    public Task<RetiroDetalleDto?> ObtenerDelOperadorAsync(Guid id, CancellationToken cancelacion = default) => ObtenerDelComercioAsync(id, cancelacion);

    public Task<RetiroDetalleDto> CancelarDelComercioAsync(Guid id, string motivo, CancellationToken cancelacion = default) => CancelarDelOperadorAsync(id, motivo, cancelacion);

    public Task<ResumenRetirosOperadorDto> ListarDelOperadorAsync(string? estado = null, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var todos = _retiros.Select(r => r.Resumen).ToList();
            var porEstado = todos.GroupBy(r => r.Estado).ToDictionary(g => g.Key, g => g.Count());
            var enCurso = todos.Count(r => r.Estado is not ("EntregadoAlComercio" or "Cancelado"));
            return Task.FromResult(new ResumenRetirosOperadorDto
            {
                PorEstado = porEstado, Total = todos.Count, EnCurso = enCurso,
                Items = todos.Where(r => estado is null || r.Estado == estado).ToList()
            });
        }
    }

    public Task<RetiroDetalleDto> RecibirEnDepositoAsync(Guid id, CancellationToken cancelacion = default) =>
        Task.FromResult(Cambiar(id, "Retirado", "EnDeposito", "RecepcionEnDeposito"));

    public Task<RetiroDetalleDto> EntregarAlComercioAsync(Guid id, string? receptor, CancellationToken cancelacion = default) =>
        Task.FromResult(Cambiar(id, "EnDeposito", "EntregadoAlComercio", "EntregaAlComercio"));

    public Task<RetiroDetalleDto> ReprogramarAsync(Guid id, CancellationToken cancelacion = default) =>
        Task.FromResult(Cambiar(id, "NoRetirado", "Solicitado", "RetiroReprogramado"));

    public Task<RetiroDetalleDto> SacarDeRutaAsync(Guid id, CancellationToken cancelacion = default) =>
        Task.FromResult(Cambiar(id, "AsignadoARuta", "Solicitado", "RetiroLiberadoDeRuta"));

    public Task<RetiroDetalleDto> CancelarDelOperadorAsync(Guid id, string motivo, CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ApiExcepcion("Debe indicar el motivo de la cancelación.");

        RetiroDetalleDto actual;
        lock (_bloqueo) actual = _retiros.FirstOrDefault(r => r.Resumen.Id == id) ?? throw new ApiExcepcion(System.Net.HttpStatusCode.NotFound, "Retiro no encontrado.");

        return Task.FromResult(Cambiar(id, actual.Resumen.Estado is "Solicitado" or "NoRetirado" ? actual.Resumen.Estado : "Solicitado", "Cancelado", "RetiroCancelado"));
    }
}
