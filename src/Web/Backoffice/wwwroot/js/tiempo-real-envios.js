/**
 * Tiempo real del estado de los envíos para todas las páginas del backoffice.
 *
 * Una sola conexión SignalR al hub de la API, autenticada con un token de corta duración emitido por el propio backoffice
 * (el operador sale de la sesión del servidor). Cuando un envío cambia de estado, la API emite "EnvioCambioEstado" y las páginas que
 * muestran estados de envío (<body data-tiempo-real-envios="true">) se actualizan SIN recargar:
 *
 *   1. Se pide la misma página (misma URL y filtros) y se compara con la que está en pantalla.
 *   2. Solo se tocan los textos, atributos y filas que cambiaron. Lo demás no se mueve: scroll, foco, pestañas y filtros quedan igual.
 *   3. No se toca nada que el usuario o un componente esté manejando: modales, campos de formulario, mapas, alertas y avisos.
 *
 * Una página puede reaccionar al cambio con el evento "tiempo-real:actualizado" (por ejemplo, para redibujar un mapa).
 * Los datos que una página guarda en variables de script se marcan con <script data-tiempo-real-datos> para que se vuelvan a evaluar.
 */
(function (window, document) {
    'use strict';

    var ESPERA_AGRUPAR_MS = 800;
    var URL_TOKEN = '/tiempo-real/token';

    // Clases que maneja JavaScript (Bootstrap y la propia página): se conservan las del elemento en pantalla, no las del servidor
    var CLASES_DE_ESTADO_UI = ['show', 'active', 'collapsed', 'collapsing', 'open', 'selected', 'is-open', 'is-invalid', 'is-valid', 'was-validated', 'modal-open'];
    // Atributos que los componentes cambian por su cuenta
    var ATRIBUTOS_DE_ESTADO_UI = ['aria-expanded', 'aria-hidden', 'aria-selected', 'aria-modal', 'tabindex', 'role'];
    // Subárboles que nunca se tocan
    var SELECTOR_INTOCABLE = '.modal, .leaflet-container, .um-mapa-leaflet, canvas, script, style, link, iframe, .alert, #contenedorNotificacionesToast, #overlayDetalleNotificacion, #panelDetalleNotificacion, [data-sin-refresco], [data-sin-refresco] *';

    var conexion = null;
    var timerAgrupar = null;
    var refrescando = false;
    var hayOtroCambio = false;
    var ultimoCambio = null;
    var escuchas = [];
    var escuchasPosicion = [];

    function paginaPideTiempoReal() {
        return document.body && document.body.getAttribute('data-tiempo-real-envios') === 'true';
    }

    function obtenerCredenciales() {
        return fetch(URL_TOKEN, { credentials: 'same-origin', headers: { 'Accept': 'application/json' }, cache: 'no-store' })
            .then(function (res) {
                if (!res.ok) throw new Error('HTTP ' + res.status);
                return res.json();
            });
    }

    // ------------------------------------------------------------------ comparación del DOM

    function esControlDeFormulario(el) {
        return /^(INPUT|TEXTAREA|SELECT|OPTION)$/.test(el.tagName);
    }

    function esIntocable(el) {
        return el.matches && el.matches(SELECTOR_INTOCABLE);
    }

    function sincronizarClases(viejo, nuevo) {
        var delServidor = (nuevo.getAttribute('class') || '').split(/\s+/).filter(function (c) {
            return c && CLASES_DE_ESTADO_UI.indexOf(c) === -1;
        });
        var delNavegador = (viejo.getAttribute('class') || '').split(/\s+/).filter(function (c) {
            return c && CLASES_DE_ESTADO_UI.indexOf(c) !== -1;
        });
        var final = delServidor.concat(delNavegador).join(' ');
        if ((viejo.getAttribute('class') || '') !== final) {
            if (final) viejo.setAttribute('class', final); else viejo.removeAttribute('class');
        }
    }

    function propiedadDeEstilo(texto, propiedad) {
        var m = new RegExp('(?:^|;)\\s*' + propiedad + '\\s*:\\s*([^;]+)', 'i').exec(texto || '');
        return m ? m[1].trim() : null;
    }

    function sincronizarEstilo(viejo, nuevo) {
        var estiloNuevo = nuevo.getAttribute('style') || '';
        var display = propiedadDeEstilo(viejo.getAttribute('style'), 'display');
        // El servidor manda anchos de barras y colores; el "display" lo alternan los scripts de la página y se respeta
        if (display !== null && propiedadDeEstilo(estiloNuevo, 'display') !== display) {
            estiloNuevo = estiloNuevo.replace(/(?:^|;)\s*display\s*:[^;]+;?/i, ';') + ';display:' + display;
        }
        if ((viejo.getAttribute('style') || '') !== estiloNuevo) {
            if (estiloNuevo) viejo.setAttribute('style', estiloNuevo); else viejo.removeAttribute('style');
        }
    }

    function sincronizarAtributos(viejo, nuevo) {
        sincronizarClases(viejo, nuevo);
        sincronizarEstilo(viejo, nuevo);

        Array.prototype.slice.call(nuevo.attributes).forEach(function (a) {
            if (a.name === 'class' || a.name === 'style' || ATRIBUTOS_DE_ESTADO_UI.indexOf(a.name) !== -1) return;
            if (viejo.getAttribute(a.name) !== a.value) viejo.setAttribute(a.name, a.value);
        });

        Array.prototype.slice.call(viejo.attributes).forEach(function (a) {
            if (a.name === 'class' || a.name === 'style' || ATRIBUTOS_DE_ESTADO_UI.indexOf(a.name) !== -1) return;
            if (!nuevo.hasAttribute(a.name)) viejo.removeAttribute(a.name);
        });
    }

    function sinComentarios(nodo) {
        return Array.prototype.slice.call(nodo.childNodes).filter(function (n) { return n.nodeType !== 8; });
    }

    function compararHijos(viejo, nuevo) {
        var vh = sinComentarios(viejo);
        var nh = sinComentarios(nuevo);

        for (var i = 0; i < nh.length; i++) {
            if (i < vh.length) {
                comparar(vh[i], nh[i]);
            } else {
                viejo.appendChild(document.importNode(nh[i], true));
            }
        }

        for (var j = vh.length - 1; j >= nh.length; j--) {
            if (vh[j].nodeType === 1 && esIntocable(vh[j])) continue;
            viejo.removeChild(vh[j]);
        }
    }

    function comparar(viejo, nuevo) {
        if (viejo.nodeType !== nuevo.nodeType || (viejo.nodeType === 1 && viejo.tagName !== nuevo.tagName)) {
            if (viejo.nodeType === 1 && esIntocable(viejo)) return;
            viejo.parentNode.replaceChild(document.importNode(nuevo, true), viejo);
            return;
        }

        if (viejo.nodeType === 3) {
            if (viejo.nodeValue !== nuevo.nodeValue) viejo.nodeValue = nuevo.nodeValue;
            return;
        }

        if (viejo.nodeType !== 1) return;
        if (esIntocable(viejo) || esControlDeFormulario(viejo)) return;

        sincronizarAtributos(viejo, nuevo);
        compararHijos(viejo, nuevo);
    }

    function evaluarDatosDeLaPagina(documentoNuevo) {
        documentoNuevo.querySelectorAll('script[data-tiempo-real-datos]').forEach(function (origen) {
            var script = document.createElement('script');
            script.textContent = origen.textContent;
            document.head.appendChild(script);
            script.remove();
        });
    }

    function reiniciarTooltips() {
        if (!window.bootstrap || !window.bootstrap.Tooltip) return;
        document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) {
            window.bootstrap.Tooltip.getOrCreateInstance(el);
        });
    }

    // ------------------------------------------------------------------ actualización de la página

    function actualizarPagina() {
        if (refrescando) {
            hayOtroCambio = true;
            return Promise.resolve();
        }

        refrescando = true;
        return fetch(window.location.href, {
            credentials: 'same-origin',
            cache: 'no-store',
            headers: { 'Accept': 'text/html', 'X-Tiempo-Real': '1' }
        }).then(function (res) {
            // Sesión vencida: el servidor redirige al login; se lleva al usuario allí en lugar de mostrarle una página ajena
            if (res.redirected && /\/Login/i.test(res.url)) {
                window.location.href = res.url;
                return null;
            }
            if (!res.ok) return null;
            return res.text();
        }).then(function (html) {
            if (!html) return;

            var nuevo = new DOMParser().parseFromString(html, 'text/html');
            var contenedorNuevo = nuevo.querySelector('.layout-contenido-principal');
            var contenedor = document.querySelector('.layout-contenido-principal');
            if (!contenedorNuevo || !contenedor) return;

            evaluarDatosDeLaPagina(nuevo);
            comparar(contenedor, contenedorNuevo);
            reiniciarTooltips();
            document.dispatchEvent(new CustomEvent('tiempo-real:actualizado', { detail: ultimoCambio }));
        }).catch(function (err) {
            // Sin red o servidor ocupado: la pantalla queda como estaba y se actualizará con el próximo cambio
            console.warn('TiempoRealEnvios: no se pudo actualizar la página (' + (err && err.message ? err.message : err) + ').');
        }).then(function () {
            refrescando = false;
            if (hayOtroCambio) {
                hayOtroCambio = false;
                programarActualizacion();
            }
        });
    }

    function programarActualizacion() {
        if (timerAgrupar) clearTimeout(timerAgrupar);
        timerAgrupar = setTimeout(function () {
            timerAgrupar = null;
            actualizarPagina();
        }, ESPERA_AGRUPAR_MS);
    }

    // ------------------------------------------------------------------ avisos y conexión

    function textoEstado(estado) {
        return String(estado || '').replace(/([a-z])([A-Z])/g, '$1 $2');
    }

    function avisarCambio(cambio) {
        if (!window.SistemaNotificaciones || !cambio) return;

        window.SistemaNotificaciones.mostrar({
            tipo: 'info',
            titulo: 'Envío actualizado',
            mensajeBreve: (cambio.codigoSeguimiento || 'Un envío') + ' pasó a: ' + textoEstado(cambio.estado),
            duracionMs: 4000
        });
    }

    function alRecibirCambio(cambio) {
        ultimoCambio = cambio;
        escuchas.forEach(function (fn) {
            try { fn(cambio); } catch (e) { /* una escucha defectuosa no debe frenar a las demás */ }
        });

        if (paginaPideTiempoReal()) {
            avisarCambio(cambio);
            programarActualizacion();
        }
    }

    // Un retiro no es un envío: no se avisa a las escuchas de envíos, solo se pone al día la página
    function alRecibirCambioRetiro(cambio) {
        if (!paginaPideTiempoReal()) return;

        if (window.SistemaNotificaciones && cambio) {
            window.SistemaNotificaciones.mostrar({
                tipo: 'info',
                titulo: 'Retiro actualizado',
                mensajeBreve: (cambio.codigoSeguimiento || 'Un retiro') + ' pasó a: ' + textoEstado(cambio.estado),
                duracionMs: 4000
            });
        }
        programarActualizacion();
    }

    function conectar() {
        if (conexion || !window.signalR || !window.signalR.HubConnectionBuilder) return;

        obtenerCredenciales().then(function (cred) {
            conexion = new window.signalR.HubConnectionBuilder()
                .withUrl(cred.hubUrl, {
                    // Token nuevo en cada (re)conexión: el de 15 minutos no debe dejar la página sorda
                    accessTokenFactory: function () {
                        return obtenerCredenciales().then(function (c) { return c.accessToken; });
                    }
                })
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .build();

            conexion.on('EnvioCambioEstado', alRecibirCambio);
            conexion.on('RetiroCambioEstado', alRecibirCambioRetiro);
            conexion.on('PosicionVehiculo', function (datos) {
                escuchasPosicion.forEach(function (fn) {
                    try { fn(datos); } catch (e) { /* una escucha defectuosa no debe frenar a las demás */ }
                });
            });
            conexion.on('IdentidadVisualActualizada', function () {
                if (paginaPideTiempoReal()) programarActualizacion();
            });

            // Lo ocurrido mientras la conexión estuvo caída no llegó como evento: se pone la página al día
            conexion.onreconnected(function () {
                if (paginaPideTiempoReal()) programarActualizacion();
            });

            return conexion.start();
        }).catch(function (err) {
            conexion = null;
            console.warn('TiempoRealEnvios: sin conexión en vivo (' + (err && err.message ? err.message : err) + ').');
        });
    }

    window.TiempoRealEnvios = {
        /** Permite a una página reaccionar al cambio de estado de un envío con su propia lógica. */
        alCambiarEstado: function (fn) {
            if (typeof fn === 'function') escuchas.push(fn);
        },
        /** Permite a una página recibir la posición en vivo de los vehículos (evento PosicionVehiculo del hub). */
        alCambiarPosicion: function (fn) {
            if (typeof fn === 'function') escuchasPosicion.push(fn);
        },
        /** Fuerza una actualización inmediata de los datos de la página. */
        actualizarAhora: actualizarPagina,
        ultimoCambio: function () { return ultimoCambio; }
    };

    document.addEventListener('DOMContentLoaded', function () {
        // El Tablero abre su propia conexión (más eventos: rutas, paradas, posiciones)
        if (window.TableroTiempoReal) return;
        if (!paginaPideTiempoReal()) return;
        conectar();
    });
})(window, document);
