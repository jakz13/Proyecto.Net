using MediatR;
using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Comercio.ImportarEnvios;

public class ImportarEnviosCommandHandler : IRequestHandler<ImportarEnviosCommand, ResultadoImportacionDto>
{
    public const int MaxFilasPorLote = 1000;

    private readonly AltaEnvioProcesador _procesador;
    private readonly ILoteImportacionRepository _loteRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ImportarEnviosCommandHandler(
        AltaEnvioProcesador procesador,
        ILoteImportacionRepository loteRepository,
        ICurrentTenant currentTenant,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _procesador = procesador;
        _loteRepository = loteRepository;
        _currentTenant = currentTenant;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<ResultadoImportacionDto> Handle(ImportarEnviosCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var comercioOperadorId = _currentTenant.ComercioOperadorId
            ?? throw new UnauthorizedAccessException("Acceso denegado: el usuario no pertenece a un comercio.");

        if (request.Filas is null || request.Filas.Count == 0)
            throw new DomainValidationException("El archivo no contiene registros para importar.");

        if (request.Filas.Count > MaxFilasPorLote)
            throw new DomainValidationException($"El archivo supera el máximo de {MaxFilasPorLote} registros por lote.");

        if (string.IsNullOrWhiteSpace(request.ClaveIdempotencia))
            throw new DomainValidationException("Indique la clave de idempotencia del lote.");

        var clave = request.ClaveIdempotencia.Trim();
        if (clave.Length > 200)
            throw new DomainValidationException("La clave de idempotencia no puede superar los 200 caracteres.");

        var lote = await _loteRepository.GetByClaveAsync(comercioOperadorId, clave, cancellationToken);
        var reimportacion = lote is not null;
        lote ??= new LoteImportacion(Guid.NewGuid(), comercioOperadorId, clave, request.Filas.Count);

        var resultados = new List<ResultadoFilaDto>(request.Filas.Count);
        var referenciasDelLote = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fila in request.Filas)
        {
            if (fila.Alta is null)
            {
                resultados.Add(new ResultadoFilaDto(fila.Numero, ResultadoFilaDto.ConError, Error: fila.ErrorLectura ?? "Registro ilegible."));
                continue;
            }

            var referencia = fila.Alta.ReferenciaExterna?.Trim();
            if (string.IsNullOrEmpty(referencia))
            {
                resultados.Add(new ResultadoFilaDto(fila.Numero, ResultadoFilaDto.ConError, Error: "La referencia del registro es obligatoria para poder reimportar sin duplicar."));
                continue;
            }

            // Una misma referencia repetida dentro del archivo se considera un error de esa fila
            if (!referenciasDelLote.Add(referencia))
            {
                resultados.Add(new ResultadoFilaDto(fila.Numero, ResultadoFilaDto.ConError, Error: $"La referencia '{referencia}' está repetida en el archivo."));
                continue;
            }

            try
            {
                // El comercio siempre sale de la sesión: el archivo no puede dar de alta envíos de otro comercio
                var alta = fila.Alta with { ComercioId = comercioOperadorId };
                var resultado = await _procesador.ProcesarAsync(_currentTenant.OperadorId, alta, lote.Id, cancellationToken);
                resultados.Add(new ResultadoFilaDto(
                    fila.Numero,
                    resultado.Duplicado ? ResultadoFilaDto.Duplicado : ResultadoFilaDto.Creado,
                    resultado.CodigoSeguimiento,
                    resultado.Tarifa));
            }
            catch (DomainValidationException ex)
            {
                resultados.Add(new ResultadoFilaDto(fila.Numero, ResultadoFilaDto.ConError, Error: ex.Message));
            }
        }

        var creados = resultados.Count(r => r.Estado == ResultadoFilaDto.Creado);
        var duplicados = resultados.Count(r => r.Estado == ResultadoFilaDto.Duplicado);
        var conError = resultados.Count(r => r.Estado == ResultadoFilaDto.ConError);

        if (reimportacion)
        {
            lote.RegistrarReimportacion(request.Filas.Count, creados + duplicados, conError, _clock.UtcNow);
        }
        else
        {
            lote.RegistrarResultado(creados + duplicados, conError);
            await _loteRepository.AddAsync(lote, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ResultadoImportacionDto(lote.Id, clave, reimportacion, request.Filas.Count, creados, duplicados, conError, resultados);
    }
}
