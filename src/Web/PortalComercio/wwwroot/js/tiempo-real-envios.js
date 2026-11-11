/**
 * Puente entre SignalR y Blazor para el estado de los envíos del comercio en tiempo real.
 * El hub solo entrega al comercio los eventos de SUS envíos (grupo propio por comercio); el token lo provee Blazor.
 */
(function (window) {
    'use strict';

    var conexion = null;

    window.TiempoRealEnviosPortal = {
        /**
         * @param referencia DotNetObjectReference con ObtenerToken() y AlCambiarEstado(codigo, estado)
         * @param urlHub URL absoluta del hub de la API
         */
        iniciar: function (referencia, urlHub) {
            if (conexion || !window.signalR || !window.signalR.HubConnectionBuilder) return Promise.resolve(false);

            conexion = new window.signalR.HubConnectionBuilder()
                .withUrl(urlHub, {
                    accessTokenFactory: function () { return referencia.invokeMethodAsync('ObtenerToken'); }
                })
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .build();

            conexion.on('EnvioCambioEstado', function (cambio) {
                referencia.invokeMethodAsync('AlCambiarEstado', cambio.codigoSeguimiento || '', cambio.estado || '');
            });

            // El comercio se vinculó (o se desvinculó) de un operador: el selector del encabezado se actualiza sin recargar
            conexion.on('OperadoresDelComercioCambiaron', function () {
                referencia.invokeMethodAsync('AlCambiarOperadores');
            });

            // Lo ocurrido mientras la conexión estuvo caída no llega como evento: se pide un refresco general
            conexion.onreconnected(function () {
                referencia.invokeMethodAsync('AlCambiarEstado', '', '');
            });

            return conexion.start().then(function () { return true; }).catch(function (err) {
                conexion = null;
                console.warn('TiempoRealEnviosPortal: sin conexión en vivo (' + (err && err.message ? err.message : err) + ').');
                return false;
            });
        },

        detener: function () {
            if (!conexion) return Promise.resolve();
            var c = conexion;
            conexion = null;
            return c.stop().catch(function () { /* ya cerrada */ });
        }
    };
})(window);
