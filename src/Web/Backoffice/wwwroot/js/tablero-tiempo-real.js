/**
 * Conexión SignalR en tiempo real para el Tablero de Operaciones (Fase 2).
 * Se autentica mediante un JWT de corta duración emitido por el propio Backoffice (?handler=TokenTiempoReal),
 * escucha CambioRuta e HitoParada, gestiona reconexión automática y vuelve a consultar ?handler=DatosEnVivo
 * tras recibir un evento o recuperar la conexión.
 */
(function (window, document) {
    'use strict';

    var SEPARADOR_RECORD = '\u001e';

    // Shim ligero compatible con signalR.HubConnectionBuilder para entornos sin salida a CDN
    if (!window.signalR) {
        function ClienteLigeroSignalR(url, opciones, demorasReconexion) {
            this._url = url;
            this._opciones = opciones || {};
            this._demoras = demorasReconexion || [0, 2000, 5000, 10000, 30000];
            this._manejadores = {};
            this._alReconectar = [];
            this._alReconectando = [];
            this._alCerrar = [];
            this._ws = null;
            this._detenidoManualmente = false;
            this._intentoReconexion = 0;
        }

        ClienteLigeroSignalR.prototype.on = function (evento, callback) {
            if (!evento || typeof callback !== 'function') return;
            var clave = evento.toLowerCase();
            if (!this._manejadores[clave]) this._manejadores[clave] = [];
            this._manejadores[clave].push(callback);
        };

        ClienteLigeroSignalR.prototype.onreconnecting = function (cb) {
            if (typeof cb === 'function') this._alReconectando.push(cb);
        };

        ClienteLigeroSignalR.prototype.onreconnected = function (cb) {
            if (typeof cb === 'function') this._alReconectar.push(cb);
        };

        ClienteLigeroSignalR.prototype.onclose = function (cb) {
            if (typeof cb === 'function') this._alCerrar.push(cb);
        };

        ClienteLigeroSignalR.prototype.start = function () {
            var self = this;
            self._detenidoManualmente = false;
            return self._conectarInterno(false);
        };

        ClienteLigeroSignalR.prototype._conectarInterno = function (esReconexion) {
            var self = this;
            var obtenerToken = typeof self._opciones.accessTokenFactory === 'function'
                ? Promise.resolve(self._opciones.accessTokenFactory())
                : Promise.resolve('');

            return obtenerToken.then(function (token) {
                var sep = self._url.indexOf('?') >= 0 ? '&' : '?';
                var urlNegociacion = self._url + sep + 'negotiateVersion=1';
                var headers = { 'Content-Type': 'text/plain;charset=UTF-8' };
                if (token) headers['Authorization'] = 'Bearer ' + token;

                var rutaFinal = self._url.replace(/\/hubs\/tablero$/, '') + '/hubs/tablero/negotiate?negotiateVersion=1';
                return fetch(rutaFinal, {
                    method: 'POST',
                    headers: headers,
                    credentials: 'include'
                }).then(function (res) {
                    if (!res.ok) throw new Error('Error en negociación SignalR: HTTP ' + res.status);
                    return res.json();
                }).then(function (neg) {
                    return new Promise(function (resolve, reject) {
                        var idConexion = encodeURIComponent(neg.connectionToken || neg.connectionId || '');
                        var esquemaWs = self._url.indexOf('https://') === 0 ? 'wss://' : 'ws://';
                        var baseSinEsquema = self._url.replace(/^https?:\/\//i, '');
                        var queryWs = '?id=' + idConexion + (token ? '&access_token=' + encodeURIComponent(token) : '');
                        var ws = new WebSocket(esquemaWs + baseSinEsquema + queryWs);
                        self._ws = ws;
                        var handshakeCompletado = false;

                        ws.onopen = function () {
                            ws.send(JSON.stringify({ protocol: 'json', version: 1 }) + SEPARADOR_RECORD);
                        };

                        ws.onmessage = function (ev) {
                            var partes = String(ev.data || '').split(SEPARADOR_RECORD);
                            for (var i = 0; i < partes.length; i++) {
                                var trama = partes[i].trim();
                                if (!trama) continue;
                                var msg;
                                try { msg = JSON.parse(trama); } catch (e) { continue; }

                                if (!handshakeCompletado) {
                                    handshakeCompletado = true;
                                    if (msg.error) {
                                        reject(new Error(msg.error));
                                        ws.close();
                                        return;
                                    }
                                    self._intentoReconexion = 0;
                                    if (esReconexion) {
                                        self._alReconectar.forEach(function (fn) { try { fn(neg.connectionId); } catch (_) { } });
                                    }
                                    resolve();
                                    continue;
                                }

                                if (msg.type === 6) {
                                    // Ping keep-alive
                                    try { ws.send(JSON.stringify({ type: 6 }) + SEPARADOR_RECORD); } catch (_) { }
                                } else if (msg.type === 1 && msg.target) {
                                    var lista = self._manejadores[String(msg.target).toLowerCase()] || [];
                                    var args = Array.isArray(msg.arguments) ? msg.arguments : [];
                                    lista.forEach(function (fn) {
                                        try { fn.apply(null, args); } catch (_) { }
                                    });
                                }
                            }
                        };

                        ws.onerror = function (err) {
                            if (!handshakeCompletado) reject(err);
                        };

                        ws.onclose = function (ev) {
                            if (!handshakeCompletado) {
                                reject(new Error('Conexión WebSocket cerrada antes del handshake.'));
                                return;
                            }
                            if (!self._detenidoManualmente) {
                                self._programarReconexion();
                            }
                        };
                    });
                });
            });
        };

        ClienteLigeroSignalR.prototype._programarReconexion = function () {
            var self = this;
            if (self._intentoReconexion >= self._demoras.length) {
                self._alCerrar.forEach(function (fn) { try { fn(); } catch (_) { } });
                return;
            }
            var espera = self._demoras[self._intentoReconexion++];
            self._alReconectando.forEach(function (fn) { try { fn(); } catch (_) { } });
            setTimeout(function () {
                self._conectarInterno(true).catch(function () {
                    self._programarReconexion();
                });
            }, espera);
        };

        function ConstructorSignalR() {
            this._url = '';
            this._opciones = {};
            this._demoras = [0, 2000, 5000, 10000, 30000];
        }

        ConstructorSignalR.prototype.withUrl = function (url, opciones) {
            this._url = url;
            this._opciones = opciones || {};
            return this;
        };

        ConstructorSignalR.prototype.withAutomaticReconnect = function (demoras) {
            if (Array.isArray(demoras)) this._demoras = demoras;
            return this;
        };

        ConstructorSignalR.prototype.configureLogging = function () {
            return this;
        };

        ConstructorSignalR.prototype.build = function () {
            return new ClienteLigeroSignalR(this._url, this._opciones, this._demoras);
        };

        window.signalR = {
            HubConnectionBuilder: ConstructorSignalR,
            LogLevel: { Warning: 3, Information: 2 }
        };
    }

    function escaparHtml(texto) {
        if (texto === null || texto === undefined) return '';
        var div = document.createElement('div');
        div.textContent = String(texto);
        return div.innerHTML;
    }

    function escaparAtributoJs(texto) {
        if (texto === null || texto === undefined) return '';
        return String(texto)
            .replace(/\\/g, '\\\\')
            .replace(/'/g, "\\'")
            .replace(/"/g, '&quot;');
    }

    function formatearHora(valorIso) {
        if (!valorIso) return '--:--';
        var fecha = new Date(valorIso);
        if (isNaN(fecha.getTime())) return String(valorIso).slice(0, 5);
        var hh = String(fecha.getHours()).padStart(2, '0');
        var mm = String(fecha.getMinutes()).padStart(2, '0');
        return hh + ':' + mm;
    }

    function actualizarEstadoBadge(estado, texto) {
        var badge = document.getElementById('estadoConexionTiempoReal');
        if (!badge) return;
        badge.textContent = texto;
        badge.className = 'badge rounded-pill small px-2 py-1 ' + (
            estado === 'conectado' ? 'bg-success-subtle text-success border border-success-subtle' :
            estado === 'reconectando' ? 'bg-warning-subtle text-dark border border-warning-subtle' :
            'bg-light text-secondary border'
        );
    }

    function filtrarVehiculosEnCliente(vehiculos) {
        var lista = Array.isArray(vehiculos) ? vehiculos.slice() : [];
        var selectFiltro = document.querySelector('select[name="filtroEstado"]');
        var inputBusqueda = document.querySelector('input[name="busqueda"]');
        var filtroEstado = selectFiltro ? selectFiltro.value : 'Todos';
        var busqueda = inputBusqueda ? inputBusqueda.value.trim().toLowerCase() : '';

        if (filtroEstado === 'SoloRiesgo') {
            lista = lista.filter(function (v) { return !!v.TieneAlertasRiesgo; });
        } else if (filtroEstado === 'EnMovimiento') {
            lista = lista.filter(function (v) { return v.EstadoOperativo === 'EnMovimiento'; });
        } else if (filtroEstado === 'Detenidos') {
            lista = lista.filter(function (v) { return v.EstadoOperativo === 'DetenidoAlerta' || (v.VelocidadKmh || 0) === 0; });
        }

        if (busqueda) {
            lista = lista.filter(function (v) {
                var mat = String(v.VehiculoMatricula || '').toLowerCase();
                var rep = String(v.RepartidorNombre || '').toLowerCase();
                var num = String(v.NumeroRuta || '').toLowerCase();
                var zon = String(v.ZonaActual || '').toLowerCase();
                var riesgos = Array.isArray(v.EnviosEnRiesgo) ? v.EnviosEnRiesgo : [];
                var coincideRiesgo = riesgos.some(function (e) {
                    return String(e.CodigoSeguimiento || '').toLowerCase().indexOf(busqueda) >= 0 ||
                           String(e.Destinatario || '').toLowerCase().indexOf(busqueda) >= 0;
                });
                return mat.indexOf(busqueda) >= 0 || rep.indexOf(busqueda) >= 0 || num.indexOf(busqueda) >= 0 || zon.indexOf(busqueda) >= 0 || coincideRiesgo;
            });
        }

        return lista;
    }

    function renderizarTablaAlertas(alertas) {
        var tbody = document.getElementById('tablaAlertasRiesgoBody') || document.querySelector('#pill-alertas tbody');
        if (!tbody) return;

        if (!Array.isArray(alertas) || alertas.length === 0) {
            tbody.innerHTML = [
                '<tr>',
                '    <td colspan="6" class="text-center py-5 text-secondary">',
                '        <div class="text-success fw-semibold mb-1">Todos los envíos se encuentran dentro del horario comprometido.</div>',
                '        <div class="small">No se registran alertas de riesgo de SLA en la flota activa.</div>',
                '    </td>',
                '</tr>'
            ].join('');
            return;
        }

        tbody.innerHTML = alertas.map(function (alerta) {
            var claseNivel = alerta.NivelRiesgo === 'Critico' ? 'estado-fallo' : 'estado-intermedio';
            return [
                '<tr>',
                '    <td>',
                '        <div class="fw-bold text-dark font-monospace">' + escaparHtml(alerta.CodigoSeguimiento) + '</div>',
                '        <div class="text-secondary small font-monospace" style="font-size: 0.725rem;">' + escaparHtml(alerta.NumeroRuta) + '</div>',
                '    </td>',
                '    <td>',
                '        <div class="fw-semibold text-dark small">' + escaparHtml(alerta.Destinatario) + '</div>',
                '        <div class="text-secondary small text-truncate" style="max-width: 240px;" title="' + escaparHtml(alerta.Direccion) + '">' + escaparHtml(alerta.Direccion) + '</div>',
                '        <div class="text-muted small" style="font-size: 0.7rem;">' + escaparHtml(alerta.Zona) + '</div>',
                '    </td>',
                '    <td>',
                '        <div class="fw-bold text-dark small">' + escaparHtml(alerta.FranjaHoraria) + '</div>',
                '        <div class="text-secondary small" style="font-size: 0.725rem;">Límite: ' + escaparHtml(formatearHora(alerta.HoraLimiteVentana)) + ' hs</div>',
                '    </td>',
                '    <td>',
                '        <span class="fw-bold ' + claseNivel + '">+' + Number(alerta.MinutosRetrasoEstimado || 0) + ' min</span>',
                '        <div class="text-secondary small mt-1" style="font-size: 0.7rem;">Est: ' + escaparHtml(formatearHora(alerta.HoraEstimadaLlegada)) + ' hs</div>',
                '    </td>',
                '    <td>',
                '        <div class="text-dark small">' + escaparHtml(alerta.CausaRiesgo) + '</div>',
                '        <div class="text-secondary small mt-1" style="font-size: 0.725rem;">Chofer: <strong class="text-dark">' + escaparHtml(alerta.RepartidorNombre) + '</strong> <span class="ms-1 font-monospace text-secondary fw-semibold">(' + escaparHtml(alerta.VehiculoMatricula) + ')</span></div>',
                '    </td>',
                '    <td class="text-end">',
                '        <div class="d-inline-flex gap-1 justify-content-end align-items-center">',
                '            <button type="button" class="btn btn-outline-secondary btn-sm p-1 px-2 d-inline-flex align-items-center justify-content-center text-dark" title="Contactar al chofer" onclick="abrirModalContacto(\'' + escaparAtributoJs(alerta.RepartidorNombre) + '\', \'' + escaparAtributoJs(alerta.RepartidorTelefono) + '\', \'' + escaparAtributoJs(alerta.VehiculoMatricula) + '\', \'' + escaparAtributoJs(alerta.CodigoSeguimiento) + '\', \'' + escaparAtributoJs(alerta.HojaDeRutaId) + '\')">',
                '                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z"></path></svg>',
                '            </button>',
                '            <a href="/Rutas/Detalle?id=' + encodeURIComponent(alerta.HojaDeRutaId || '') + '" class="btn btn-outline-dark btn-sm p-1 px-2 d-inline-flex align-items-center justify-content-center" title="Reasignar en la hoja de ruta">',
                '                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polyline points="17 1 21 5 17 9"></polyline><path d="M3 11V9a4 4 0 0 1 4-4h14"></path><polyline points="7 23 3 19 7 15"></polyline><path d="M21 13v2a4 4 0 0 1-4 4H3"></path></svg>',
                '            </a>',
                '        </div>',
                '    </td>',
                '</tr>'
            ].join('');
        }).join('');
    }

    function renderizarTablaFlota(vehiculosFiltrados) {
        var tbody = document.getElementById('tablaFlotaBody') || document.querySelector('#pill-flota tbody');
        var contadorFlota = document.getElementById('contadorUnidadesFlota');
        if (contadorFlota) {
            contadorFlota.textContent = vehiculosFiltrados.length + ' unidades desplegadas';
        }
        if (!tbody) return;

        tbody.innerHTML = vehiculosFiltrados.map(function (veh) {
            var paradaHtml = veh.ParadaActualCodigo
                ? '<span class="font-monospace fw-bold estado-intermedio small">' + escaparHtml(veh.ParadaActualCodigo) + '</span>'
                : '<span class="text-muted small">Sin parada activa</span>';

            var telemetriaHtml;
            if (veh.TieneAlertasRiesgo) {
                telemetriaHtml = '<span class="estado-fallo fw-semibold small">Alerta SLA (' + Number(veh.CantidadEnviosEnRiesgo || 0) + ')</span>';
            } else if (veh.EstadoOperativo === 'EnMovimiento') {
                telemetriaHtml = '<span class="estado-entregado fw-semibold small">En Movimiento (' + Number(veh.VelocidadKmh || 0) + ' km/h)</span>';
            } else {
                telemetriaHtml = '<span class="estado-intermedio fw-semibold small">Detenido (0 km/h)</span>';
            }

            var pct = Number(veh.PorcentajeProgreso || 0);
            var barraClase = veh.TieneAlertasRiesgo ? 'bg-danger' : 'bg-dark';

            return [
                '<tr>',
                '    <td>',
                '        <div class="fw-bold text-dark font-monospace">' + escaparHtml(veh.VehiculoMatricula) + '</div>',
                '        <div class="text-secondary small font-monospace" style="font-size: 0.725rem;">' + escaparHtml(veh.NumeroRuta) + '</div>',
                '        <div class="text-muted small" style="font-size: 0.7rem;">' + escaparHtml(veh.VehiculoModelo) + '</div>',
                '    </td>',
                '    <td>',
                '        <div class="fw-semibold text-dark small">' + escaparHtml(veh.RepartidorNombre) + '</div>',
                '        <div class="text-secondary small font-monospace" style="font-size: 0.725rem;">' + escaparHtml(veh.RepartidorTelefono) + '</div>',
                '    </td>',
                '    <td>',
                '        <div class="fw-semibold text-dark small">' + escaparHtml(veh.ZonaActual) + '</div>',
                '        <div class="text-secondary small text-truncate" style="max-width: 220px;" title="' + escaparHtml(veh.DireccionAproximada) + '">' + escaparHtml(veh.DireccionAproximada) + '</div>',
                '    </td>',
                '    <td>' + paradaHtml + '</td>',
                '    <td>',
                '        <div class="d-flex justify-content-between align-items-center small mb-1">',
                '            <span class="fw-semibold text-dark">' + Number(veh.ParadasCompletadas || 0) + ' / ' + Number(veh.TotalParadas || 0) + '</span>',
                '            <span class="text-secondary small">' + pct + '%</span>',
                '        </div>',
                '        <div class="progress" style="height: 6px;">',
                '            <div class="progress-bar ' + barraClase + '" style="width: ' + pct + '%;"></div>',
                '        </div>',
                '    </td>',
                '    <td>',
                '        <div>' + telemetriaHtml + '</div>',
                '        <div class="text-secondary small mt-1" style="font-size: 0.7rem;">Bat: <strong class="text-dark">' + Number(veh.NivelBateriaPct || 0) + '%</strong> · GPS sincronizado</div>',
                '    </td>',
                '    <td class="text-end">',
                '        <div class="d-inline-flex gap-1 justify-content-end align-items-center">',
                '            <button type="button" class="btn btn-outline-secondary btn-sm p-1 px-2 d-inline-flex align-items-center justify-content-center text-dark" title="Contactar al chofer" onclick="abrirModalContacto(\'' + escaparAtributoJs(veh.RepartidorNombre) + '\', \'' + escaparAtributoJs(veh.RepartidorTelefono) + '\', \'' + escaparAtributoJs(veh.VehiculoMatricula) + '\', null, \'' + escaparAtributoJs(veh.RepartidorId) + '\')">',
                '                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z"></path></svg>',
                '            </button>',
                '            <a href="/Rutas/Detalle?id=' + encodeURIComponent(veh.HojaDeRutaId || '') + '" class="btn btn-outline-dark btn-sm p-1 px-2 d-inline-flex align-items-center justify-content-center" title="Ver itinerario de ruta">',
                '                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polygon points="1 6 1 22 8 18 16 22 23 18 23 2 16 6 8 2 1 6"></polygon><line x1="8" y1="2" x2="8" y2="18"></line><line x1="16" y1="6" x2="16" y2="22"></line></svg>',
                '            </a>',
                '        </div>',
                '    </td>',
                '</tr>'
            ].join('');
        }).join('');
    }

    function aplicarResumenEnDom(resumen) {
        if (!resumen) return;

        var elRutas = document.getElementById('metricaRutasEnEjecucion');
        var elRepartidores = document.getElementById('metricaRepartidoresEnCalle');
        var elTransito = document.getElementById('metricaEnviosEnTransito');
        var elRiesgo = document.getElementById('metricaEnviosEnRiesgo');
        var tarjetaRiesgo = document.getElementById('tarjetaEnviosEnRiesgo');
        var contenedorValorRiesgo = document.getElementById('contenedorValorEnviosEnRiesgo');
        var badgeAtencion = document.getElementById('badgeAtencionRiesgo');

        var totalRutas = Number(resumen.TotalRutasEnEjecucion ?? resumen.totalRutasEnEjecucion ?? 0);
        var totalRepartidores = Number(resumen.TotalRepartidoresEnCalle ?? resumen.totalRepartidoresEnCalle ?? 0);
        var totalTransito = Number(resumen.TotalEnviosEnTransito ?? resumen.totalEnviosEnTransito ?? 0);
        var totalRiesgo = Number(resumen.TotalEnviosEnRiesgo ?? resumen.totalEnviosEnRiesgo ?? 0);

        if (elRutas) elRutas.textContent = String(totalRutas);
        if (elRepartidores) elRepartidores.textContent = String(totalRepartidores);
        if (elTransito) elTransito.textContent = String(totalTransito);
        if (elRiesgo) elRiesgo.textContent = String(totalRiesgo);

        if (tarjetaRiesgo) {
            if (totalRiesgo > 0) {
                tarjetaRiesgo.classList.add('border-start', 'border-danger', 'border-4');
            } else {
                tarjetaRiesgo.classList.remove('border-start', 'border-danger', 'border-4');
            }
        }
        if (contenedorValorRiesgo) {
            contenedorValorRiesgo.classList.toggle('text-danger', totalRiesgo > 0);
            contenedorValorRiesgo.classList.toggle('text-dark', totalRiesgo === 0);
        }
        if (badgeAtencion) {
            badgeAtencion.style.display = totalRiesgo > 0 ? 'inline' : 'none';
        }

        var vehiculos = resumen.Vehiculos || resumen.vehiculos || [];
        var alertas = resumen.AlertasRiesgo || resumen.alertasRiesgo || [];

        window.datosVehiculos = vehiculos;
        var contadorMapa = document.getElementById('contadorUnidadesMapa');
        if (contadorMapa) {
            contadorMapa.textContent = vehiculos.length + ' Unidades Rastreadas';
        }

        renderizarTablaAlertas(alertas);
        renderizarTablaFlota(filtrarVehiculosEnCliente(vehiculos));

        if (typeof window.dibujarMapa === 'function') {
            window.dibujarMapa();
        }

        if (window.vehiculoSeleccionadoEnMapa && typeof window.seleccionarEnMapa === 'function') {
            var idSeleccionado = window.vehiculoSeleccionadoEnMapa.HojaDeRutaId;
            var actualizado = vehiculos.find(function (v) { return v.HojaDeRutaId === idSeleccionado; }) || vehiculos[0];
            if (actualizado) {
                window.seleccionarEnMapa(actualizado);
            }
        }
    }

    var timerRefresco = null;
    function programarActualizacionTablero() {
        if (timerRefresco) clearTimeout(timerRefresco);
        timerRefresco = setTimeout(function () {
            actualizarTableroEnVivo();
        }, 200);
    }

    function actualizarTableroEnVivo() {
        return fetch('?handler=DatosEnVivo', {
            method: 'GET',
            headers: { 'Accept': 'application/json' },
            credentials: 'same-origin'
        })
        .then(function (res) {
            if (!res.ok) throw new Error('HTTP ' + res.status);
            return res.json();
        })
        .then(function (datos) {
            aplicarResumenEnDom(datos);
            return datos;
        })
        .catch(function (err) {
            console.warn('TableroTiempoReal: no se pudo refrescar DatosEnVivo:', err);
        });
    }

    function obtenerTokenTiempoReal() {
        return fetch('?handler=TokenTiempoReal', {
            method: 'GET',
            headers: { 'Accept': 'application/json' },
            credentials: 'same-origin'
        })
        .then(function (res) {
            if (!res.ok) throw new Error('No se pudo obtener el token SignalR');
            return res.json();
        })
        .then(function (payload) {
            return payload && payload.accessToken ? payload.accessToken : '';
        });
    }

    function iniciarConexionSignalR(urlHubConfigurada) {
        if (!window.signalR || !window.signalR.HubConnectionBuilder) return;

        var urlHub = urlHubConfigurada || 'http://localhost:5000/hubs/tablero';
        var conexion = new window.signalR.HubConnectionBuilder()
            .withUrl(urlHub, {
                accessTokenFactory: obtenerTokenTiempoReal
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .build();

        conexion.on('CambioRuta', function () {
            programarActualizacionTablero();
        });

        conexion.on('HitoParada', function () {
            programarActualizacionTablero();
        });

        if (typeof conexion.onreconnecting === 'function') {
            conexion.onreconnecting(function () {
                actualizarEstadoBadge('reconectando', 'Reconectando...');
            });
        }

        if (typeof conexion.onreconnected === 'function') {
            conexion.onreconnected(function () {
                actualizarEstadoBadge('conectado', 'En vivo');
                // Recuperar cualquier cambio ocurrido mientras la conexión estuvo interrumpida
                actualizarTableroEnVivo();
            });
        }

        if (typeof conexion.onclose === 'function') {
            conexion.onclose(function () {
                actualizarEstadoBadge('desconectado', 'Sin conexión en vivo');
            });
        }

        conexion.start()
            .then(function () {
                actualizarEstadoBadge('conectado', 'En vivo');
            })
            .catch(function () {
                actualizarEstadoBadge('desconectado', 'Modo local');
            });

        window.conexionTableroSignalR = conexion;
    }

    window.TableroTiempoReal = {
        iniciar: iniciarConexionSignalR,
        actualizar: actualizarTableroEnVivo
    };
})(window, document);
