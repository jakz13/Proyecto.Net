using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria para la gestión de flota de vehículos y padrón de repartidores (UC-07, RF-06).
/// </summary>
public class MockFlotaApi : IFlotaApi
{
    private readonly List<VehiculoDto> _vehiculos;
    private readonly List<RepartidorDto> _repartidores;
    private readonly object _bloqueo = new();

    public MockFlotaApi()
    {
        var vehiculo1Id = Guid.Parse("22222222-2222-2222-2222-222222222201");
        var vehiculo2Id = Guid.Parse("22222222-2222-2222-2222-222222222202");
        var vehiculo3Id = Guid.Parse("22222222-2222-2222-2222-222222222203");
        var vehiculo4Id = Guid.Parse("22222222-2222-2222-2222-222222222204");
        var vehiculo5Id = Guid.Parse("22222222-2222-2222-2222-222222222205");

        var repartidor1Id = Guid.Parse("33333333-3333-3333-3333-333333333301");
        var repartidor2Id = Guid.Parse("33333333-3333-3333-3333-333333333302");
        var repartidor3Id = Guid.Parse("33333333-3333-3333-3333-333333333303");
        var repartidor4Id = Guid.Parse("33333333-3333-3333-3333-333333333304");
        var repartidor5Id = Guid.Parse("33333333-3333-3333-3333-333333333305");

        _vehiculos = new List<VehiculoDto>
        {
            new()
            {
                Id = vehiculo1Id,
                OperadorId = "OP-01",
                Patente = "STX 4821",
                Tipo = "Furgón Mediano",
                CapacidadPeso = 850m,
                CapacidadVolumen = 6.2m,
                Activo = true,
                RepartidorAsignadoId = repartidor1Id,
                RepartidorAsignadoNombre = "Diego Rossi"
            },
            new()
            {
                Id = vehiculo2Id,
                OperadorId = "OP-01",
                Patente = "STX 3190",
                Tipo = "Utilitario Ligero",
                CapacidadPeso = 450m,
                CapacidadVolumen = 3.5m,
                Activo = true,
                RepartidorAsignadoId = repartidor2Id,
                RepartidorAsignadoNombre = "Matías Silvera"
            },
            new()
            {
                Id = vehiculo3Id,
                OperadorId = "OP-01",
                Patente = "STX 1055",
                Tipo = "Moto Cargo",
                CapacidadPeso = 60m,
                CapacidadVolumen = 0.4m,
                Activo = true,
                RepartidorAsignadoId = repartidor3Id,
                RepartidorAsignadoNombre = "Lucía Fernández"
            },
            new()
            {
                Id = vehiculo4Id,
                OperadorId = "OP-01",
                Patente = "STX 9088",
                Tipo = "Camioneta Pick-up",
                CapacidadPeso = 750m,
                CapacidadVolumen = 4.8m,
                Activo = true,
                RepartidorAsignadoId = null,
                RepartidorAsignadoNombre = null
            },
            new()
            {
                Id = vehiculo5Id,
                OperadorId = "OP-01",
                Patente = "STX 6214",
                Tipo = "Furgón Grande",
                CapacidadPeso = 1400m,
                CapacidadVolumen = 11.0m,
                Activo = false,
                RepartidorAsignadoId = null,
                RepartidorAsignadoNombre = null
            }
        };

        _repartidores = new List<RepartidorDto>
        {
            new()
            {
                Id = repartidor1Id,
                UsuarioId = Guid.NewGuid(),
                OperadorId = "OP-01",
                NombreCompleto = "Diego Rossi",
                Email = "diego.rossi@translogistica.uy",
                Documento = "4.120.554-1",
                Telefono = "+598 99 234 567",
                Estado = "Disponible",
                VehiculoAsignadoId = vehiculo1Id,
                VehiculoPatente = "STX 4821",
                VehiculoTipo = "Furgón Mediano",
                VehiculoCapacidad = "850 kg / 6.2 m³"
            },
            new()
            {
                Id = repartidor2Id,
                UsuarioId = Guid.NewGuid(),
                OperadorId = "OP-01",
                NombreCompleto = "Matías Silvera",
                Email = "matias.silvera@translogistica.uy",
                Documento = "3.987.412-8",
                Telefono = "+598 98 441 223",
                Estado = "En Ruta",
                VehiculoAsignadoId = vehiculo2Id,
                VehiculoPatente = "STX 3190",
                VehiculoTipo = "Utilitario Ligero",
                VehiculoCapacidad = "450 kg / 3.5 m³"
            },
            new()
            {
                Id = repartidor3Id,
                UsuarioId = Guid.NewGuid(),
                OperadorId = "OP-01",
                NombreCompleto = "Lucía Fernández",
                Email = "lucia.fernandez@translogistica.uy",
                Documento = "4.654.321-9",
                Telefono = "+598 91 887 766",
                Estado = "Disponible",
                VehiculoAsignadoId = vehiculo3Id,
                VehiculoPatente = "STX 1055",
                VehiculoTipo = "Moto Cargo",
                VehiculoCapacidad = "60 kg / 0.4 m³"
            },
            new()
            {
                Id = repartidor4Id,
                UsuarioId = Guid.NewGuid(),
                OperadorId = "OP-01",
                NombreCompleto = "Esteban Larrosa",
                Email = "esteban.larrosa@translogistica.uy",
                Documento = "4.331.098-5",
                Telefono = "+598 94 550 119",
                Estado = "Disponible",
                VehiculoAsignadoId = null,
                VehiculoPatente = null,
                VehiculoTipo = null,
                VehiculoCapacidad = null
            },
            new()
            {
                Id = repartidor5Id,
                UsuarioId = Guid.NewGuid(),
                OperadorId = "OP-01",
                NombreCompleto = "Gonzalo Méndez",
                Email = "gonzalo.mendez@translogistica.uy",
                Documento = "5.012.345-6",
                Telefono = "+598 99 778 990",
                Estado = "Inactivo",
                VehiculoAsignadoId = null,
                VehiculoPatente = null,
                VehiculoTipo = null,
                VehiculoCapacidad = null
            }
        };
    }

    // =========================================================================
    // Operaciones de Vehículos
    // =========================================================================

    public Task<IReadOnlyList<VehiculoDto>> ListarVehiculosAsync(bool? soloActivos = null, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var consulta = _vehiculos.AsEnumerable();
            if (soloActivos.HasValue)
            {
                consulta = consulta.Where(v => v.Activo == soloActivos.Value);
            }

            var resultado = consulta
                .OrderByDescending(v => v.Activo)
                .ThenBy(v => v.Patente)
                .Select(v => v with { })
                .ToList();

            return Task.FromResult<IReadOnlyList<VehiculoDto>>(resultado);
        }
    }

    public Task<VehiculoDto?> ObtenerVehiculoPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var vehiculo = _vehiculos.FirstOrDefault(v => v.Id == id);
            return Task.FromResult(vehiculo != null ? vehiculo with { } : null);
        }
    }

    public Task<VehiculoDto> CrearVehiculoAsync(CrearVehiculoDto dto, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var patenteNormalizada = dto.Patente.Trim().ToUpperInvariant();
            if (_vehiculos.Any(v => v.Patente.Equals(patenteNormalizada, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ApiExcepcion($"Ya existe un vehículo registrado con la patente '{patenteNormalizada}'.");
            }

            if (dto.CapacidadPeso <= 0)
            {
                throw new ApiExcepcion("La capacidad en peso debe ser mayor a 0 kg.");
            }

            if (dto.CapacidadVolumen <= 0)
            {
                throw new ApiExcepcion("La capacidad en volumen debe ser mayor a 0 m³.");
            }

            var nuevo = new VehiculoDto
            {
                Id = Guid.NewGuid(),
                OperadorId = "OP-01",
                Patente = patenteNormalizada,
                Tipo = dto.Tipo.Trim(),
                CapacidadPeso = Math.Round(dto.CapacidadPeso, 2),
                CapacidadVolumen = Math.Round(dto.CapacidadVolumen, 2),
                Activo = true,
                RepartidorAsignadoId = null,
                RepartidorAsignadoNombre = null
            };

            _vehiculos.Add(nuevo);
            return Task.FromResult(nuevo with { });
        }
    }

    public Task<VehiculoDto> ActualizarVehiculoAsync(ActualizarVehiculoDto dto, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var indice = _vehiculos.FindIndex(v => v.Id == dto.Id);
            if (indice < 0)
            {
                throw new ApiExcepcion("El vehículo seleccionado no existe en el sistema.");
            }

            var patenteNormalizada = dto.Patente.Trim().ToUpperInvariant();
            if (_vehiculos.Any(v => v.Id != dto.Id && v.Patente.Equals(patenteNormalizada, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ApiExcepcion($"La patente '{patenteNormalizada}' ya se encuentra asignada a otro vehículo.");
            }

            var actual = _vehiculos[indice];
            var actualizado = actual with
            {
                Patente = patenteNormalizada,
                Tipo = dto.Tipo.Trim(),
                CapacidadPeso = Math.Round(dto.CapacidadPeso, 2),
                CapacidadVolumen = Math.Round(dto.CapacidadVolumen, 2),
                Activo = dto.Activo
            };

            // Si se desactiva el vehículo, desasociar del repartidor que lo tuviese asignado
            if (!dto.Activo && actualizado.RepartidorAsignadoId.HasValue)
            {
                DesasignarVehiculoInterno(actualizado.Id);
                actualizado = actualizado with { RepartidorAsignadoId = null, RepartidorAsignadoNombre = null };
            }

            _vehiculos[indice] = actualizado;
            SincronizarDatosVehiculoEnRepartidores(actualizado);

            return Task.FromResult(actualizado with { });
        }
    }

    public Task CambiarEstadoVehiculoAsync(Guid id, bool activo, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var indice = _vehiculos.FindIndex(v => v.Id == id);
            if (indice < 0)
            {
                throw new ApiExcepcion("El vehículo solicitado no fue encontrado.");
            }

            var vehiculo = _vehiculos[indice];
            if (!activo && vehiculo.RepartidorAsignadoId.HasValue)
            {
                DesasignarVehiculoInterno(vehiculo.Id);
                vehiculo = vehiculo with { RepartidorAsignadoId = null, RepartidorAsignadoNombre = null };
            }

            _vehiculos[indice] = vehiculo with { Activo = activo };
            return Task.CompletedTask;
        }
    }

    // =========================================================================
    // Operaciones de Repartidores
    // =========================================================================

    public Task<IReadOnlyList<RepartidorDto>> ListarRepartidoresAsync(string? estado = null, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var consulta = _repartidores.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(estado))
            {
                consulta = consulta.Where(r => r.Estado.Equals(estado.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            var resultado = consulta
                .OrderBy(r => r.NombreCompleto)
                .Select(r => r with { })
                .ToList();

            return Task.FromResult<IReadOnlyList<RepartidorDto>>(resultado);
        }
    }

    public Task<RepartidorDto?> ObtenerRepartidorPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var repartidor = _repartidores.FirstOrDefault(r => r.Id == id);
            return Task.FromResult(repartidor != null ? repartidor with { } : null);
        }
    }

    public Task<RepartidorDto> CrearRepartidorAsync(CrearRepartidorDto dto, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var documentoNormalizado = dto.Documento.Trim();
            if (_repartidores.Any(r => r.Documento.Equals(documentoNormalizado, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ApiExcepcion($"Ya existe un repartidor registrado con el documento '{documentoNormalizado}'.");
            }

            var emailNormalizado = dto.Email.Trim().ToLowerInvariant();
            if (_repartidores.Any(r => r.Email.Equals(emailNormalizado, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ApiExcepcion($"El correo '{emailNormalizado}' ya está en uso por otro repartidor.");
            }

            VehiculoDto? vehiculoElegido = null;
            if (dto.VehiculoAsignadoId.HasValue)
            {
                vehiculoElegido = _vehiculos.FirstOrDefault(v => v.Id == dto.VehiculoAsignadoId.Value && v.Activo);
                if (vehiculoElegido == null)
                {
                    throw new ApiExcepcion("El vehículo seleccionado no se encuentra disponible o está inactivo.");
                }

                // Si el vehículo ya estaba asignado a otro repartidor, desasociarlo primero
                DesasignarVehiculoInterno(vehiculoElegido.Id);
            }

            var nuevoRepartidor = new RepartidorDto
            {
                Id = Guid.NewGuid(),
                UsuarioId = Guid.NewGuid(),
                OperadorId = "OP-01",
                NombreCompleto = dto.NombreCompleto.Trim(),
                Email = emailNormalizado,
                Documento = documentoNormalizado,
                Telefono = dto.Telefono.Trim(),
                Estado = string.IsNullOrWhiteSpace(dto.Estado) ? "Disponible" : dto.Estado.Trim(),
                VehiculoAsignadoId = vehiculoElegido?.Id,
                VehiculoPatente = vehiculoElegido?.Patente,
                VehiculoTipo = vehiculoElegido?.Tipo,
                VehiculoCapacidad = vehiculoElegido != null ? $"{vehiculoElegido.CapacidadPeso:N0} kg / {vehiculoElegido.CapacidadVolumen:N1} m³" : null
            };

            _repartidores.Add(nuevoRepartidor);

            // Actualizar vehículo para reflejar el nuevo repartidor
            if (vehiculoElegido != null)
            {
                var indiceV = _vehiculos.FindIndex(v => v.Id == vehiculoElegido.Id);
                if (indiceV >= 0)
                {
                    _vehiculos[indiceV] = _vehiculos[indiceV] with
                    {
                        RepartidorAsignadoId = nuevoRepartidor.Id,
                        RepartidorAsignadoNombre = nuevoRepartidor.NombreCompleto
                    };
                }
            }

            return Task.FromResult(nuevoRepartidor with { });
        }
    }

    public Task<RepartidorDto> ActualizarRepartidorAsync(ActualizarRepartidorDto dto, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var indice = _repartidores.FindIndex(r => r.Id == dto.Id);
            if (indice < 0)
            {
                throw new ApiExcepcion("El repartidor no fue encontrado en el sistema.");
            }

            var documentoNormalizado = dto.Documento.Trim();
            if (_repartidores.Any(r => r.Id != dto.Id && r.Documento.Equals(documentoNormalizado, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ApiExcepcion($"El documento '{documentoNormalizado}' ya pertenece a otro repartidor.");
            }

            var emailNormalizado = dto.Email.Trim().ToLowerInvariant();
            if (_repartidores.Any(r => r.Id != dto.Id && r.Email.Equals(emailNormalizado, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ApiExcepcion($"El correo '{emailNormalizado}' ya está en uso por otro repartidor.");
            }

            var actual = _repartidores[indice];

            // Gestionar cambio o retiro de vehículo
            VehiculoDto? nuevoVehiculo = null;
            if (dto.VehiculoAsignadoId.HasValue && dto.VehiculoAsignadoId != actual.VehiculoAsignadoId)
            {
                nuevoVehiculo = _vehiculos.FirstOrDefault(v => v.Id == dto.VehiculoAsignadoId.Value && v.Activo);
                if (nuevoVehiculo == null)
                {
                    throw new ApiExcepcion("El vehículo seleccionado para asignar no está disponible o está inactivo.");
                }

                // Desasignar vehículo previo si tenía uno
                if (actual.VehiculoAsignadoId.HasValue)
                {
                    DesasignarVehiculoInterno(actual.VehiculoAsignadoId.Value);
                }

                // Desasignar el nuevo vehículo de quien lo tuviera asignado
                DesasignarVehiculoInterno(nuevoVehiculo.Id);

                // Asignar al vehículo este repartidor
                var idxV = _vehiculos.FindIndex(v => v.Id == nuevoVehiculo.Id);
                if (idxV >= 0)
                {
                    _vehiculos[idxV] = _vehiculos[idxV] with
                    {
                        RepartidorAsignadoId = actual.Id,
                        RepartidorAsignadoNombre = dto.NombreCompleto.Trim()
                    };
                }
            }
            else if (!dto.VehiculoAsignadoId.HasValue && actual.VehiculoAsignadoId.HasValue)
            {
                // Se removió el vehículo asignado
                DesasignarVehiculoInterno(actual.VehiculoAsignadoId.Value);
            }
            else if (dto.VehiculoAsignadoId.HasValue && dto.VehiculoAsignadoId == actual.VehiculoAsignadoId)
            {
                nuevoVehiculo = _vehiculos.FirstOrDefault(v => v.Id == dto.VehiculoAsignadoId.Value);
            }

            var actualizado = actual with
            {
                NombreCompleto = dto.NombreCompleto.Trim(),
                Email = emailNormalizado,
                Documento = documentoNormalizado,
                Telefono = dto.Telefono.Trim(),
                Estado = dto.Estado.Trim(),
                VehiculoAsignadoId = nuevoVehiculo?.Id,
                VehiculoPatente = nuevoVehiculo?.Patente,
                VehiculoTipo = nuevoVehiculo?.Tipo,
                VehiculoCapacidad = nuevoVehiculo != null ? $"{nuevoVehiculo.CapacidadPeso:N0} kg / {nuevoVehiculo.CapacidadVolumen:N1} m³" : null
            };

            _repartidores[indice] = actualizado;

            // Actualizar nombre en el vehículo si cambió
            if (actualizado.VehiculoAsignadoId.HasValue)
            {
                var idxV = _vehiculos.FindIndex(v => v.Id == actualizado.VehiculoAsignadoId.Value);
                if (idxV >= 0)
                {
                    _vehiculos[idxV] = _vehiculos[idxV] with { RepartidorAsignadoNombre = actualizado.NombreCompleto };
                }
            }

            return Task.FromResult(actualizado with { });
        }
    }

    public Task AsignarVehiculoAsync(Guid repartidorId, Guid? vehiculoId, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var indiceR = _repartidores.FindIndex(r => r.Id == repartidorId);
            if (indiceR < 0)
            {
                throw new ApiExcepcion("El repartidor solicitado no existe.");
            }

            var repartidor = _repartidores[indiceR];

            // Si ya tenía vehículo, liberarlo
            if (repartidor.VehiculoAsignadoId.HasValue)
            {
                DesasignarVehiculoInterno(repartidor.VehiculoAsignadoId.Value);
            }

            VehiculoDto? vehiculoNuevo = null;
            if (vehiculoId.HasValue)
            {
                var indiceV = _vehiculos.FindIndex(v => v.Id == vehiculoId.Value);
                if (indiceV < 0 || !_vehiculos[indiceV].Activo)
                {
                    throw new ApiExcepcion("El vehículo seleccionado no existe o no se encuentra activo.");
                }

                // Si el nuevo vehículo ya estaba en otro repartidor, desasociarlo de ese otro repartidor
                DesasignarVehiculoInterno(vehiculoId.Value);

                vehiculoNuevo = _vehiculos[indiceV];
                _vehiculos[indiceV] = vehiculoNuevo with
                {
                    RepartidorAsignadoId = repartidor.Id,
                    RepartidorAsignadoNombre = repartidor.NombreCompleto
                };
            }

            _repartidores[indiceR] = repartidor with
            {
                VehiculoAsignadoId = vehiculoNuevo?.Id,
                VehiculoPatente = vehiculoNuevo?.Patente,
                VehiculoTipo = vehiculoNuevo?.Tipo,
                VehiculoCapacidad = vehiculoNuevo != null ? $"{vehiculoNuevo.CapacidadPeso:N0} kg / {vehiculoNuevo.CapacidadVolumen:N1} m³" : null
            };

            return Task.CompletedTask;
        }
    }

    public Task CambiarEstadoRepartidorAsync(Guid repartidorId, string nuevoEstado, CancellationToken cancelacion = default)
    {
        lock (_bloqueo)
        {
            var indice = _repartidores.FindIndex(r => r.Id == repartidorId);
            if (indice < 0)
            {
                throw new ApiExcepcion("El repartidor solicitado no existe.");
            }

            _repartidores[indice] = _repartidores[indice] with { Estado = nuevoEstado.Trim() };
            return Task.CompletedTask;
        }
    }

    // =========================================================================
    // Métodos Auxiliares Internos
    // =========================================================================

    private void DesasignarVehiculoInterno(Guid vehiculoId)
    {
        // Limpiar en vehículos
        var idxV = _vehiculos.FindIndex(v => v.Id == vehiculoId);
        if (idxV >= 0)
        {
            _vehiculos[idxV] = _vehiculos[idxV] with
            {
                RepartidorAsignadoId = null,
                RepartidorAsignadoNombre = null
            };
        }

        // Limpiar en repartidores
        for (var i = 0; i < _repartidores.Count; i++)
        {
            if (_repartidores[i].VehiculoAsignadoId == vehiculoId)
            {
                _repartidores[i] = _repartidores[i] with
                {
                    VehiculoAsignadoId = null,
                    VehiculoPatente = null,
                    VehiculoTipo = null,
                    VehiculoCapacidad = null
                };
            }
        }
    }

    private void SincronizarDatosVehiculoEnRepartidores(VehiculoDto vehiculo)
    {
        for (var i = 0; i < _repartidores.Count; i++)
        {
            if (_repartidores[i].VehiculoAsignadoId == vehiculo.Id)
            {
                _repartidores[i] = _repartidores[i] with
                {
                    VehiculoPatente = vehiculo.Patente,
                    VehiculoTipo = vehiculo.Tipo,
                    VehiculoCapacidad = $"{vehiculo.CapacidadPeso:N0} kg / {vehiculo.CapacidadVolumen:N1} m³"
                };
            }
        }
    }
}
