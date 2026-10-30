/**
 * Tiempo real del estado de los envíos para todas las páginas del backoffice (RF-12, RF-29).
 *
 * Una sola conexión SignalR al hub de la API, autenticada con un token de corta duración emitido por el propio backoffice
 * (el operador sale de la sesión del servidor). Cuando un envío cambia de estado, la API emite "EnvioCambioEstado" y las páginas que
 * muestran estados de envío (<body data-tiempo-real-envios="true">) se ponen al día solas.
 *
 * Reglas para no estorbar al usuario:
 *  - Nunca se recarga mientras haya un modal abierto, un campo en edición o un formulario con cambios sin guardar:
 *    se espera y se reintenta apenas se libera.
 *  - Varios eventos seguidos se juntan en un único refresco.
 *  - La página del Tablero tiene su propia conexión; aquí no se abre una segunda.
 */
(function (window, document) {
    'use strict';

    var ESPERA_AGRUPAR_MS = 1200;
    var ESPERA_REINTENTO_MS = 3000;
    var URL_TOKEN = '/tiempo-real/token';

    var conexion = null;
    var urlHub = null;
    var timerRefresco = null;
    var pendiente = false;
    var ultimoCambio = null;
    var escuchas = [];

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

    function formularioConCambios() {
        var formularios = document.querySelectorAll('form');
        for (var i = 0; i < formularios.length; i++) {
            if (formularios[i].getAttribute('data-sin-refresco') === 'true') return true;
            if (formularios[i].dataset && formularios[i].dataset.modificado === 'true') return true;
        }
        return false;
    }

    function usuarioOcupado() {
        if (document.querySelector('.modal.show')) return true;

        var activo = document.activeElement;
        if (activo && /^(INPUT|TEXTAREA|SELECT)$/.test(activo.tagName) && activo.type !== 'button' && activo.type !== 'submit') return true;

        return formularioConCambios();
    }

    function refrescarPagina() {
        window.location.reload();
    }

    function intentarRefrescar() {
        timerRefresco = null;
        if (!pendiente) return;

        if (usuarioOcupado()) {
            timerRefresco = setTimeout(intentarRefrescar, ESPERA_REINTENTO_MS);
            return;
        }

        pendiente = false;
        refrescarPagina();
    }

    function programarRefresco() {
        pendiente = true;
        if (timerRefresco) clearTimeout(timerRefresco);
        timerRefresco = setTimeout(intentarRefrescar, ESPERA_AGRUPAR_MS);
    }

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
            programarRefresco();
        }
    }

    function conectar() {
        if (conexion || !window.signalR || !window.signalR.HubConnectionBuilder) return;

        obtenerCredenciales().then(function (cred) {
            urlHub = cred.hubUrl;

            conexion = new window.signalR.HubConnectionBuilder()
                .withUrl(urlHub, {
                    // Token nuevo en cada (re)conexión: el de 15 minutos no debe dejar la página sorda
                    accessTokenFactory: function () {
                        return obtenerCredenciales().then(function (c) { return c.accessToken; });
                    }
                })
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .build();

            conexion.on('EnvioCambioEstado', alRecibirCambio);

            // Lo ocurrido mientras la conexión estuvo caída no llegó como evento: se pone la página al día
            conexion.onreconnected(function () {
                if (paginaPideTiempoReal()) programarRefresco();
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
        ultimoCambio: function () { return ultimoCambio; }
    };

    document.addEventListener('DOMContentLoaded', function () {
        // El Tablero abre su propia conexión (más eventos: rutas, paradas, posiciones)
        if (window.TableroTiempoReal) return;
        if (!paginaPideTiempoReal()) return;
        conectar();
    });

    // Un formulario editado por el usuario marca la página como "en edición" para no pisarle lo escrito
    document.addEventListener('input', function (e) {
        var form = e.target && e.target.closest ? e.target.closest('form') : null;
        if (form && form.getAttribute('method') && form.getAttribute('method').toLowerCase() === 'post') {
            form.dataset.modificado = 'true';
        }
    }, true);

    // Al liberarse un modal se retoma el refresco que haya quedado esperando
    document.addEventListener('hidden.bs.modal', function () {
        if (pendiente && !timerRefresco) timerRefresco = setTimeout(intentarRefrescar, 500);
    });
})(window, document);
