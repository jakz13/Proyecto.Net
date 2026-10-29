/**
 * Componente Universal de Notificaciones Flotantes y Panel de Detalle
 * Compatible con Backoffice (Razor Pages), PortalComercio (Blazor) y SeguimientoPublico.
 */
(function (window) {
    'use strict';

    var MAX_NOTIFICACIONES = 5;
    var DURACION_DEFAULT_MS = 7000;
    var notificacionesActivas = [];
    var contadorId = 0;

    // Asegurar estructura del DOM al cargar
    function asegurarContenedores() {
        var contenedor = document.getElementById('contenedorNotificacionesToast');
        if (!contenedor) {
            contenedor = document.createElement('div');
            contenedor.id = 'contenedorNotificacionesToast';
            contenedor.setAttribute('aria-live', 'polite');
            document.body.appendChild(contenedor);
        }

        var overlay = document.getElementById('overlayDetalleNotificacion');
        if (!overlay) {
            overlay = document.createElement('div');
            overlay.id = 'overlayDetalleNotificacion';
            overlay.className = 'panel-notificacion-overlay';
            overlay.onclick = cerrarPanelDetalle;
            document.body.appendChild(overlay);
        }

        var panel = document.getElementById('panelDetalleNotificacion');
        if (!panel) {
            panel = document.createElement('div');
            panel.id = 'panelDetalleNotificacion';
            panel.className = 'panel-notificacion-lateral';
            panel.innerHTML = [
                '<div class="panel-notificacion-header">',
                '    <div class="d-flex align-items-center gap-2">',
                '        <div id="panelDetalleIcono" class="toast-icono"></div>',
                '        <h5 class="mb-0 fw-bold fs-6 text-dark" id="panelDetalleTitulo">Detalle de Notificación</h5>',
                '    </div>',
                '    <button type="button" class="btn-close" aria-label="Cerrar" onclick="window.SistemaNotificaciones.cerrarPanel()"></button>',
                '</div>',
                '<div class="panel-notificacion-body" id="panelDetalleContenido">',
                '</div>',
                '<div class="panel-notificacion-footer">',
                '    <button type="button" class="btn btn-outline-secondary btn-sm" onclick="window.SistemaNotificaciones.cerrarPanel()">Cerrar</button>',
                '</div>'
            ].join('');
            document.body.appendChild(panel);
        }

        return contenedor;
    }

    function obtenerIconoSvg(tipo) {
        switch (tipo) {
            case 'exito':
                return '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>';
            case 'error':
                return '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>';
            case 'alerta':
                return '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>';
            default:
                return '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12.01" y2="16"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>';
        }
    }

    function mostrar(opciones) {
        asegurarContenedores();
        var contenedor = document.getElementById('contenedorNotificacionesToast');
        if (!contenedor) return;

        var tipo = opciones.tipo || 'info';
        var titulo = opciones.titulo || 'Notificación del Sistema';
        var mensajeBreve = opciones.mensajeBreve || opciones.mensaje || '';
        var detalle = opciones.detalle || opciones.detalleHtml || mensajeBreve;
        var duracionMs = opciones.duracionMs || DURACION_DEFAULT_MS;
        var id = 'notif_' + (++contadorId);

        var datosNotificacion = {
            id: id,
            tipo: tipo,
            titulo: titulo,
            mensajeBreve: mensajeBreve,
            detalle: detalle,
            estadisticas: opciones.estadisticas || null,
            metadatos: opciones.metadatos || null,
            fecha: new Date(),
            duracionMs: duracionMs
        };

        var tarjeta = document.createElement('div');
        tarjeta.id = id;
        tarjeta.className = 'toast-notificacion tipo-' + tipo;
        tarjeta.setAttribute('role', 'alert');
        tarjeta.innerHTML = [
            '<div class="toast-cuerpo">',
            '    <div class="toast-icono">' + obtenerIconoSvg(tipo) + '</div>',
            '    <div class="toast-contenido">',
            '        <div class="toast-titulo">' + escaparHtml(titulo) + '</div>',
            '        <div class="toast-mensaje">' + escaparHtml(mensajeBreve) + '</div>',
            '        <div class="toast-indicador-clic">Clic para ver detalle &rarr;</div>',
            '    </div>',
            '    <button type="button" class="toast-btn-cerrar" title="Cerrar" aria-label="Cerrar">&times;</button>',
            '</div>',
            '<div class="toast-progreso-barra">',
            '    <div class="toast-progreso-relleno" style="width: 100%;"></div>',
            '</div>'
        ].join('');

        tarjeta.addEventListener('click', function (e) {
            if (e.target.closest('.toast-btn-cerrar')) {
                e.stopPropagation();
                removerNotificacion(id);
                return;
            }
            abrirPanelDetalle(datosNotificacion);
        });

        var btnCerrar = tarjeta.querySelector('.toast-btn-cerrar');
        if (btnCerrar) {
            btnCerrar.addEventListener('click', function (e) {
                e.stopPropagation();
                removerNotificacion(id);
            });
        }

        var tiempoRestante = duracionMs;
        var tiempoInicio = Date.now();
        var timerId = null;
        var barraProgreso = tarjeta.querySelector('.toast-progreso-relleno');

        function iniciarTemporizador() {
            tiempoInicio = Date.now();
            barraProgreso.style.transitionDuration = tiempoRestante + 'ms';
            barraProgreso.style.width = '0%';
            timerId = setTimeout(function () {
                removerNotificacion(id);
            }, tiempoRestante);
        }

        function pausarTemporizador() {
            if (timerId) {
                clearTimeout(timerId);
                timerId = null;
                var tiempoTranscurrido = Date.now() - tiempoInicio;
                tiempoRestante = Math.max(0, tiempoRestante - tiempoTranscurrido);
                var anchoActual = (tiempoRestante / duracionMs) * 100;
                barraProgreso.style.transitionDuration = '0ms';
                barraProgreso.style.width = anchoActual + '%';
            }
        }

        tarjeta.addEventListener('mouseenter', pausarTemporizador);
        tarjeta.addEventListener('mouseleave', function () {
            if (tiempoRestante > 0) {
                iniciarTemporizador();
            }
        });

        if (contenedor.firstChild) {
            contenedor.insertBefore(tarjeta, contenedor.firstChild);
        } else {
            contenedor.appendChild(tarjeta);
        }

        notificacionesActivas.unshift({ id: id, elemento: tarjeta, datos: datosNotificacion, timerId: timerId });
        iniciarTemporizador();

        while (notificacionesActivas.length > MAX_NOTIFICACIONES) {
            var antigua = notificacionesActivas.pop();
            if (antigua && antigua.elemento && antigua.elemento.parentNode) {
                antigua.elemento.classList.add('toast-saliendo');
                (function (el) {
                    setTimeout(function () {
                        if (el.parentNode) el.parentNode.removeChild(el);
                    }, 300);
                })(antigua.elemento);
            }
        }

        return id;
    }

    function removerNotificacion(id) {
        var index = notificacionesActivas.findIndex(function (n) { return n.id === id; });
        if (index !== -1) {
            var item = notificacionesActivas.splice(index, 1)[0];
            if (item.elemento) {
                item.elemento.classList.add('toast-saliendo');
                setTimeout(function () {
                    if (item.elemento && item.elemento.parentNode) {
                        item.elemento.parentNode.removeChild(item.elemento);
                    }
                }, 300);
            }
        }
    }

    function abrirPanelDetalle(datos) {
        asegurarContenedores();
        var overlay = document.getElementById('overlayDetalleNotificacion');
        var panel = document.getElementById('panelDetalleNotificacion');
        var icono = document.getElementById('panelDetalleIcono');
        var titulo = document.getElementById('panelDetalleTitulo');
        var contenido = document.getElementById('panelDetalleContenido');

        if (!panel || !overlay || !contenido) return;

        icono.className = 'toast-icono tipo-' + datos.tipo;
        icono.innerHTML = obtenerIconoSvg(datos.tipo);
        titulo.textContent = datos.titulo;

        var html = [];

        html.push('<div class="d-flex justify-content-between align-items-center mb-3 pb-2 border-bottom">');
        html.push('    <span class="badge bg-light text-dark border text-uppercase" style="font-size: 0.7rem; letter-spacing: 0.05em;">' + datos.tipo + '</span>');
        html.push('    <span class="text-secondary small font-monospace">' + datos.fecha.toLocaleTimeString() + ' · ' + datos.fecha.toLocaleDateString() + '</span>');
        html.push('</div>');

        html.push('<div class="p-3 bg-light rounded-3 border mb-3">');
        html.push('    <div class="fw-bold text-dark mb-1">' + escaparHtml(datos.titulo) + '</div>');
        html.push('    <div class="text-secondary small">' + escaparHtml(datos.detalle) + '</div>');
        html.push('</div>');

        if (datos.estadisticas) {
            var stats = datos.estadisticas;
            html.push('<div class="small fw-bold text-uppercase text-secondary mb-2" style="font-size: 0.7rem; letter-spacing: 0.05em;">Métricas del Proceso</div>');
            html.push('<div class="panel-estadisticas-grid">');
            html.push('    <div class="panel-stat-card">');
            html.push('        <div class="panel-stat-label">Total Registros</div>');
            html.push('        <div class="panel-stat-valor text-dark">' + (stats.total || 0) + '</div>');
            html.push('    </div>');
            html.push('    <div class="panel-stat-card">');
            html.push('        <div class="panel-stat-label">Procesados con Éxito</div>');
            html.push('        <div class="panel-stat-valor text-success">' + (stats.exitos || 0) + '</div>');
            html.push('    </div>');
            html.push('    <div class="panel-stat-card">');
            html.push('        <div class="panel-stat-label">Duplicados / Omitidos</div>');
            html.push('        <div class="panel-stat-valor text-warning" style="color: #d97706 !important;">' + (stats.duplicados || 0) + '</div>');
            html.push('    </div>');
            html.push('    <div class="panel-stat-card">');
            html.push('        <div class="panel-stat-label">Con Errores</div>');
            html.push('        <div class="panel-stat-valor text-danger">' + (stats.errores || 0) + '</div>');
            html.push('    </div>');
            html.push('</div>');
        }

        if (datos.metadatos && Object.keys(datos.metadatos).length > 0) {
            html.push('<div class="small fw-bold text-uppercase text-secondary mb-2" style="font-size: 0.7rem; letter-spacing: 0.05em;">Información Técnica</div>');
            html.push('<div class="table-responsive mb-3">');
            html.push('    <table class="table table-sm table-bordered align-middle small mb-0">');
            html.push('        <tbody>');
            for (var clave in datos.metadatos) {
                if (datos.metadatos.hasOwnProperty(clave)) {
                    html.push('<tr>');
                    html.push('    <th class="table-light text-secondary" style="width: 35%;">' + escaparHtml(clave) + '</th>');
                    html.push('    <td class="font-monospace text-break text-dark">' + escaparHtml(datos.metadatos[clave]) + '</td>');
                    html.push('</tr>');
                }
            }
            html.push('        </tbody>');
            html.push('    </table>');
            html.push('</div>');
        }

        contenido.innerHTML = html.join('');
        overlay.classList.add('activo');
        panel.classList.add('abierto');
    }

    function cerrarPanelDetalle() {
        var overlay = document.getElementById('overlayDetalleNotificacion');
        var panel = document.getElementById('panelDetalleNotificacion');
        if (overlay) overlay.classList.remove('activo');
        if (panel) panel.classList.remove('abierto');
    }

    function escaparHtml(texto) {
        if (!texto) return '';
        var div = document.createElement('div');
        div.innerText = texto;
        return div.innerHTML;
    }

    window.SistemaNotificaciones = {
        mostrar: mostrar,
        cerrar: removerNotificacion,
        abrirDetalle: abrirPanelDetalle,
        cerrarPanel: cerrarPanelDetalle
    };

    window.notificarExito = function (titulo, mensaje, opciones) {
        var opts = Object.assign({}, opciones || {}, { tipo: 'exito', titulo: titulo, mensajeBreve: mensaje });
        return mostrar(opts);
    };

    window.notificarError = function (titulo, mensaje, opciones) {
        var opts = Object.assign({}, opciones || {}, { tipo: 'error', titulo: titulo, mensajeBreve: mensaje });
        return mostrar(opts);
    };

    window.notificarAlerta = function (titulo, mensaje, opciones) {
        var opts = Object.assign({}, opciones || {}, { tipo: 'alerta', titulo: titulo, mensajeBreve: mensaje });
        return mostrar(opts);
    };

    window.notificarInfo = function (titulo, mensaje, opciones) {
        var opts = Object.assign({}, opciones || {}, { tipo: 'info', titulo: titulo, mensajeBreve: mensaje });
        return mostrar(opts);
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', asegurarContenedores);
    } else {
        asegurarContenedores();
    }

})(window);
