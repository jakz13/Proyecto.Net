/**
 * Puente entre SignalR y Blazor para el seguimiento público en vivo (sin autenticación).
 * El destinatario se suscribe solo al código de su envío y recibe una señal cuando cambia; la página vuelve a consultar el
 * endpoint público (que aplica la privacidad), así que por este canal no viaja ningún dato del envío.
 */
(function (window) {
    'use strict';

    var conexion = null;
    var referencia = null;
    var codigos = [];

    function suscribirTodos() {
        return Promise.all(codigos.map(function (codigo) {
            return conexion.invoke('Seguir', codigo).catch(function () { /* se reintenta en la próxima reconexión */ });
        }));
    }

    window.TiempoRealSeguimiento = {
        iniciar: function (ref, urlHub) {
            referencia = ref;
            if (conexion) return Promise.resolve(true);
            if (!window.signalR || !window.signalR.HubConnectionBuilder) return Promise.resolve(false);

            conexion = new window.signalR.HubConnectionBuilder()
                .withUrl(urlHub)
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .build();

            conexion.on('EnvioActualizado', function () {
                if (referencia) referencia.invokeMethodAsync('AlActualizar');
            });

            // Al reconectar el servidor olvida las suscripciones: se renuevan y se pone la página al día
            conexion.onreconnected(function () {
                suscribirTodos().then(function () {
                    if (referencia) referencia.invokeMethodAsync('AlActualizar');
                });
            });

            return conexion.start().then(function () { return true; }).catch(function (err) {
                conexion = null;
                console.warn('TiempoRealSeguimiento: sin conexión en vivo (' + (err && err.message ? err.message : err) + ').');
                return false;
            });
        },

        seguir: function (codigo) {
            if (!conexion) return Promise.resolve(false);

            // Solo se sigue el envío que el usuario está mirando: los códigos anteriores se sueltan
            var anteriores = codigos.filter(function (c) { return c !== codigo; });
            codigos = [codigo];
            anteriores.forEach(function (c) { conexion.invoke('Dejar', c).catch(function () { /* ya desconectado */ }); });

            return conexion.invoke('Seguir', codigo).then(function (ok) { return ok === true; }).catch(function () { return false; });
        },

        detener: function () {
            var c = conexion;
            conexion = null;
            codigos = [];
            referencia = null;
            return c ? c.stop().catch(function () { /* ya cerrada */ }) : Promise.resolve();
        }
    };
})(window);
