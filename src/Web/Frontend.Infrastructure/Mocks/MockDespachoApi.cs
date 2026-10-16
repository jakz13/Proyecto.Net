using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Despacho;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria para el control de carga y despacho de hojas de ruta (UC-09).
/// </summary>
public class MockDespachoApi : IDespachoApi
{
    private static readonly List<string> _avisosDespachadorHistoricos = new();

    public MockDespachoApi()
    {
        MockRutasApi.AsegurarInicializacion();
    }

    /// <summary>
    /// Consulta las hojas de ruta listas para verificar carga física y salida del depósito.
    /// </summary>
    public Task<IReadOnlyList<HojaDeRutaDto>> ListarRutasPendientesDespachoAsync(CancellationToken cancelacion = default)
    {
        lock (MockRutasApi.BloqueoCompartido)
        {
            var rutasPendientes = MockRutasApi.RepositorioHojasDeRuta
                .Where(r => r.Estado == "Liberada" || r.Estado == "EnCarga")
                .OrderBy(r => r.Fecha)
                .ThenBy(r => r.ViajeNumero)
                .ToList();

            return Task.FromResult<IReadOnlyList<HojaDeRutaDto>>(rutasPendientes);
        }
    }

    /// <summary>
    /// Obtiene el detalle de la hoja de ruta y la lista de bultos a verificar en el muelle de carga.
    /// </summary>
    public Task<VerificacionDespachoDto?> ObtenerControlDespachoAsync(Guid hojaDeRutaId, CancellationToken cancelacion = default)
    {
        lock (MockRutasApi.BloqueoCompartido)
        {
            var ruta = MockRutasApi.RepositorioHojasDeRuta.FirstOrDefault(r => r.Id == hojaDeRutaId);
            if (ruta == null)
                return Task.FromResult<VerificacionDespachoDto?>(null);

            // Verificar si el repartidor tiene otra hoja de ruta despachada sin rendir
            var rutaSinRendir = MockRutasApi.RepositorioHojasDeRuta
                .FirstOrDefault(r => r.RepartidorId == ruta.RepartidorId &&
                                     r.Id != ruta.Id &&
                                     (r.Estado == "Despachada" || r.Estado == "EnTransito"));

            var bultos = ruta.Paradas.Select(p => new BultoVerificacionDto
            {
                BultoId = Guid.NewGuid(),
                EnvioId = p.EnvioId,
                CodigoSeguimiento = p.CodigoSeguimiento,
                CodigoBulto = $"BLT-{p.CodigoSeguimiento.Replace("TRK-", "")}-01",
                Destinatario = p.Destinatario,
                Direccion = p.Direccion,
                Zona = p.Zona,
                PesoKg = p.PesoKg,
                VolumenM3 = p.VolumenM3,
                EscaneadoEnDeposito = true,
                CargadoEfectivo = true
            }).ToList();

            var control = new VerificacionDespachoDto
            {
                HojaDeRutaId = ruta.Id,
                NumeroRuta = ruta.NumeroRuta,
                Fecha = ruta.Fecha,
                ViajeNumero = ruta.ViajeNumero,
                Turno = ruta.Turno,
                EstadoRuta = ruta.Estado,
                RepartidorId = ruta.RepartidorId,
                RepartidorNombre = ruta.RepartidorNombre,
                VehiculoId = ruta.VehiculoId,
                VehiculoMatricula = ruta.VehiculoMatricula,
                VehiculoModelo = ruta.VehiculoModelo,
                CapacidadPesoMaxKg = ruta.CapacidadPesoMaxKg,
                CapacidadVolumenMaxM3 = ruta.CapacidadVolumenMaxM3,
                TieneRutaPendienteDeRendir = rutaSinRendir != null,
                NumeroRutaPendienteRendicion = rutaSinRendir?.NumeroRuta,
                Bultos = bultos
            };

            return Task.FromResult<VerificacionDespachoDto?>(control);
        }
    }

    /// <summary>
    /// Confirma la salida del vehículo. Los bultos cargados pasan a En Tránsito y los no cargados vuelven a Depósito.
    /// </summary>
    public Task<ResultadoDespachoDto> ConfirmarDespachoAsync(ConfirmarDespachoDto dto, string operarioUsuario, CancellationToken cancelacion = default)
    {
        lock (MockRutasApi.BloqueoCompartido)
        {
            var ruta = MockRutasApi.RepositorioHojasDeRuta.FirstOrDefault(r => r.Id == dto.HojaDeRutaId);
            if (ruta == null)
            {
                return Task.FromResult(new ResultadoDespachoDto
                {
                    Exitoso = false,
                    Mensaje = "La hoja de ruta especificada no existe.",
                    Errores = new() { "Hoja de ruta no encontrada en el sistema." }
                });
            }

            if (ruta.Estado == "Despachada" || ruta.Estado == "EnTransito")
            {
                return Task.FromResult(new ResultadoDespachoDto
                {
                    Exitoso = false,
                    Mensaje = $"La hoja de ruta '{ruta.NumeroRuta}' ya fue despachada previamente.",
                    Errores = new() { "La ruta ya se encuentra en tránsito o despachada." }
                });
            }

            // Precondición: El repartidor no debe tener otra hoja de ruta despachada sin rendir
            var rutaPreviaSinRendir = MockRutasApi.RepositorioHojasDeRuta
                .FirstOrDefault(r => r.RepartidorId == ruta.RepartidorId &&
                                     r.Id != ruta.Id &&
                                     (r.Estado == "Despachada" || r.Estado == "EnTransito"));

            if (dto.SimularRutaPreviaSinRendir || rutaPreviaSinRendir != null)
            {
                var rutaBloqueante = rutaPreviaSinRendir?.NumeroRuta ?? "HR-2026-0098";
                return Task.FromResult(new ResultadoDespachoDto
                {
                    Exitoso = false,
                    Mensaje = $"No es posible despachar: el repartidor '{ruta.RepartidorNombre}' tiene la hoja de ruta '{rutaBloqueante}' despachada sin rendir.",
                    Errores = new()
                    {
                        $"Bloqueo por rendición pendiente: el repartidor debe liquidar y rendir los bultos de la ruta previa ({rutaBloqueante}) en depósito antes de iniciar una nueva salida."
                    }
                });
            }

            var enviosNoCargadosIds = dto.BultosNoCargados.Select(b => b.EnvioId).ToHashSet();
            var bultosReintegradosList = new List<string>();
            var avisosAlDespachadorList = new List<string>();

            // Procesar bultos no cargados: retirarlos de la ruta y devolverlos a depósito
            var paradasConservadas = new List<ParadaRutaDto>();
            decimal pesoCargado = 0;
            decimal volumenCargado = 0;
            int nuevoOrden = 1;

            foreach (var parada in ruta.Paradas)
            {
                if (enviosNoCargadosIds.Contains(parada.EnvioId))
                {
                    var infoNoCarga = dto.BultosNoCargados.First(b => b.EnvioId == parada.EnvioId);
                    var motivoLegible = infoNoCarga.Motivo switch
                    {
                        "NoEncontrado" => "Bulto no encontrado en depósito",
                        "Danado" => "Bulto dañado durante la carga",
                        "DireccionIlegible" => "Etiqueta / dirección ilegible",
                        "Discrepancia" => "Discrepancia de peso / volumen físico",
                        _ => infoNoCarga.Motivo
                    };

                    // Reintegrar al catálogo de pendientes en depósito para reprogramar en otro viaje
                    MockRutasApi.RepositorioEnviosEnDeposito.Add(new EnvioPendienteRutaDto
                    {
                        Id = parada.EnvioId,
                        CodigoSeguimiento = parada.CodigoSeguimiento,
                        Destinatario = parada.Destinatario,
                        Direccion = parada.Direccion,
                        Zona = parada.Zona,
                        FranjaHoraria = parada.FranjaHoraria,
                        Turno = ruta.Turno,
                        PesoKg = parada.PesoKg,
                        VolumenM3 = parada.VolumenM3,
                        Observaciones = $"Reintegrado desde {ruta.NumeroRuta}: {motivoLegible} ({infoNoCarga.Detalle})",
                        SelloModificacion = Guid.NewGuid().ToString("N")
                    });

                    var textoAviso = $"Envío {parada.CodigoSeguimiento} retirado de ruta {ruta.NumeroRuta} por: {motivoLegible}. Reintegrado a depósito para reprogramación.";
                    bultosReintegradosList.Add($"{parada.CodigoSeguimiento} - {parada.Destinatario} ({motivoLegible})");
                    avisosAlDespachadorList.Add(textoAviso);
                    _avisosDespachadorHistoricos.Add(textoAviso);
                }
                else
                {
                    // El envío fue efectivamente cargado y pasa a En Tránsito
                    paradasConservadas.Add(parada with
                    {
                        Orden = nuevoOrden++,
                        Estado = "EnTransito"
                    });

                    pesoCargado += parada.PesoKg;
                    volumenCargado += parada.VolumenM3;
                }
            }

            // Actualizar la hoja de ruta al estado Despachada
            var indice = MockRutasApi.RepositorioHojasDeRuta.IndexOf(ruta);
            var rutaDespachada = ruta with
            {
                Estado = "Despachada",
                FechaDespacho = DateTime.UtcNow,
                OperarioDespacho = operarioUsuario,
                CantidadParadas = paradasConservadas.Count,
                PesoTotalKg = pesoCargado,
                VolumenTotalM3 = volumenCargado,
                Paradas = paradasConservadas,
                SelloModificacion = Guid.NewGuid().ToString("N")
            };

            MockRutasApi.RepositorioHojasDeRuta[indice] = rutaDespachada;

            return Task.FromResult(new ResultadoDespachoDto
            {
                Exitoso = true,
                Mensaje = $"Hoja de ruta '{ruta.NumeroRuta}' despachada con éxito. El vehículo inició su recorrido.",
                HojaDeRutaId = ruta.Id,
                NumeroRuta = ruta.NumeroRuta,
                EnviosEnTransitoCount = paradasConservadas.Count,
                EnviosReintegradosDepositoCount = bultosReintegradosList.Count,
                EnviosReintegrados = bultosReintegradosList,
                AvisosAlDespachador = avisosAlDespachadorList
            });
        }
    }

    /// <summary>
    /// Registra la rendición de una hoja de ruta previa en depósito para habilitar al repartidor.
    /// </summary>
    public Task<bool> RendirRutaPreviaAsync(string numeroRuta, CancellationToken cancelacion = default)
    {
        lock (MockRutasApi.BloqueoCompartido)
        {
            var rutaPrevia = MockRutasApi.RepositorioHojasDeRuta.FirstOrDefault(r => r.NumeroRuta == numeroRuta);
            if (rutaPrevia == null)
                return Task.FromResult(false);

            var indice = MockRutasApi.RepositorioHojasDeRuta.IndexOf(rutaPrevia);
            MockRutasApi.RepositorioHojasDeRuta[indice] = rutaPrevia with
            {
                Estado = "Finalizada"
            };

            return Task.FromResult(true);
        }
    }
}
