/**
 * Módulo de Mapas Interactivos con Leaflet adaptado a la identidad visual de Última Milla.
 * Soporta renderizado de paradas de entrega, depósito central y camiones de reparto en vivo
 * tanto en el Portal del Comercio (Blazor WASM) como en el Backoffice (Razor Pages).
 */
(function (window) {
    'use strict';

    // Registro de instancias activas de mapas por ID de contenedor DOM
    var instanciasMapas = {};
    var marcadoresPorContenedor = {};
    var vistaModificadaPorUsuario = {};

    // Coordenadas base del Depósito Logístico Central (Montevideo - Zona Goes / Reducto)
    var DEPOSITO_CENTRAL = {
        lat: -34.8836,
        lng: -56.1865,
        nombre: 'Depósito Logístico Central',
        direccion: 'Gral. Flores 2450, Montevideo'
    };

    // Catálogo determinista de coordenadas reales en tierra firme para calles y zonas de Montevideo / Área Metropolitana
    var CATALOGO_DIRECCIONES = [
        { clave: 'bvar. artigas 1234', lat: -34.9082, lng: -56.1642 },
        { clave: 'bvar. artigas', lat: -34.8965, lng: -56.1658 },
        { clave: 'av. rivera 2890', lat: -34.9035, lng: -56.1485 },
        { clave: 'av. rivera 3200', lat: -34.9022, lng: -56.1415 },
        { clave: 'av. rivera', lat: -34.8980, lng: -56.1320 },
        { clave: 'colonia 1540', lat: -34.9018, lng: -56.1815 },
        { clave: 'colonia 1640', lat: -34.9015, lng: -56.1788 },
        { clave: 'colonia', lat: -34.9020, lng: -56.1830 },
        { clave: 'camino carrasco 6120', lat: -34.8745, lng: -56.0780 },
        { clave: 'camino carrasco', lat: -34.8760, lng: -56.0920 },
        { clave: 'sarandí 450', lat: -34.9068, lng: -56.2045 },
        { clave: 'sarandi 450', lat: -34.9068, lng: -56.2045 },
        { clave: 'sarandí', lat: -34.9065, lng: -56.2035 },
        { clave: 'bvar. españa 2210', lat: -34.9115, lng: -56.1610 },
        { clave: 'bvar. españa', lat: -34.9125, lng: -56.1565 },
        { clave: 'giannattasio', lat: -34.8380, lng: -55.9820 },
        { clave: 'av. brasil 3120', lat: -34.9145, lng: -56.1475 },
        { clave: 'av. brasil 2890', lat: -34.9132, lng: -56.1520 },
        { clave: 'av. brasil', lat: -34.9128, lng: -56.1535 },
        { clave: 'san fructuoso 1150', lat: -34.8732, lng: -56.1975 },
        { clave: 'san fructuoso', lat: -34.8725, lng: -56.1995 },
        { clave: '18 de julio 1420', lat: -34.9055, lng: -56.1845 },
        { clave: '18 de julio 1250', lat: -34.9058, lng: -56.1882 },
        { clave: '18 de julio', lat: -34.9052, lng: -56.1810 },
        { clave: 'san josé 1110', lat: -34.9072, lng: -56.1912 },
        { clave: 'san jose 1110', lat: -34.9072, lng: -56.1912 },
        { clave: 'agraciada 3105', lat: -34.8695, lng: -56.2040 },
        { clave: 'agraciada 3100', lat: -34.8692, lng: -56.2042 },
        { clave: 'agraciada 3400', lat: -34.8658, lng: -56.2062 },
        { clave: 'agraciada', lat: -34.8750, lng: -56.1980 },
        { clave: 'ellauri 350', lat: -34.9188, lng: -56.1565 },
        { clave: 'sarmiento 2210', lat: -34.9165, lng: -56.1590 },
        { clave: '21 de setiembre 2700', lat: -34.9158, lng: -56.1545 },
        { clave: '21 de setiembre 2714', lat: -34.9156, lng: -56.1540 },
        { clave: 'libertad 2500', lat: -34.9138, lng: -56.1562 },
        { clave: 'benito blanco 1120', lat: -34.9142, lng: -56.1468 },
        { clave: 'gabriel pereira 3120', lat: -34.9098, lng: -56.1455 },
        { clave: 'scoseria 2540', lat: -34.9152, lng: -56.1528 },
        { clave: 'lucas obes 1240', lat: -34.8638, lng: -56.2078 },
        { clave: 'zapicán 2500', lat: -34.8702, lng: -56.2025 },
        { clave: 'av. millán 3800', lat: -34.8588, lng: -56.2048 },
        { clave: 'garcía morales 1120', lat: -34.8632, lng: -56.2015 },
        { clave: 'guayabos 1720', lat: -34.9042, lng: -56.1778 },
        { clave: 'aconcagua 4810', lat: -34.8935, lng: -56.0965 },
        { clave: '8 de octubre 3812', lat: -34.8785, lng: -56.1425 },
        { clave: '8 de octubre', lat: -34.8840, lng: -56.1520 },
        { clave: 'av. italia', lat: -34.8895, lng: -56.1260 },
        { clave: 'propios', lat: -34.8760, lng: -56.1560 },
        { clave: 'bulevar artigas', lat: -34.8965, lng: -56.1658 }
    ];

    var CATALOGO_ZONAS = [
        { clave: 'pocitos', lat: -34.9125, lng: -56.1525 },
        { clave: 'punta carretas', lat: -34.9195, lng: -56.1585 },
        { clave: 'centro', lat: -34.9055, lng: -56.1885 },
        { clave: 'cordón', lat: -34.9025, lng: -56.1765 },
        { clave: 'cordon', lat: -34.9025, lng: -56.1765 },
        { clave: 'ciudad vieja', lat: -34.9065, lng: -56.2050 },
        { clave: 'prado', lat: -34.8645, lng: -56.2040 },
        { clave: 'bella vista', lat: -34.8710, lng: -56.2010 },
        { clave: 'paso molino', lat: -34.8595, lng: -56.2140 },
        { clave: 'malvín', lat: -34.8925, lng: -56.0990 },
        { clave: 'malvin', lat: -34.8925, lng: -56.0990 },
        { clave: 'carrasco', lat: -34.8845, lng: -56.0610 },
        { clave: 'unión', lat: -34.8795, lng: -56.1395 },
        { clave: 'union', lat: -34.8795, lng: -56.1395 },
        { clave: 'maroñas', lat: -34.8640, lng: -56.1280 },
        { clave: 'canelones', lat: -34.8410, lng: -55.9940 },
        { clave: 'ciudad de la costa', lat: -34.8380, lng: -55.9820 }
    ];

    function hashTexto(texto) {
        var h = 2166136261;
        var str = String(texto || 'um');
        for (var i = 0; i < str.length; i++) {
            h ^= str.charCodeAt(i);
            h = Math.imul(h, 16777619);
        }
        return Math.abs(h);
    }

    function resolverCoordenadasMontevideo(direccion, zona, semillaExtra) {
        var dirNorm = String(direccion || '').toLowerCase().trim();
        var zonaNorm = String(zona || '').toLowerCase().trim();
        var h = hashTexto(dirNorm + '|' + (semillaExtra || ''));
        var offsetLat = ((h % 140) - 70) * 0.000045;
        var offsetLng = (((h >> 4) % 140) - 70) * 0.000045;

        for (var i = 0; i < CATALOGO_DIRECCIONES.length; i++) {
            if (dirNorm.indexOf(CATALOGO_DIRECCIONES[i].clave) !== -1) {
                return {
                    lat: CATALOGO_DIRECCIONES[i].lat + offsetLat,
                    lng: CATALOGO_DIRECCIONES[i].lng + offsetLng
                };
            }
        }

        var combinado = dirNorm + ' ' + zonaNorm;
        for (var j = 0; j < CATALOGO_ZONAS.length; j++) {
            if (combinado.indexOf(CATALOGO_ZONAS[j].clave) !== -1) {
                return {
                    lat: CATALOGO_ZONAS[j].lat + offsetLat * 2.2,
                    lng: CATALOGO_ZONAS[j].lng + offsetLng * 2.2
                };
            }
        }

        // Posición segura en el área urbana central de Montevideo (evitando el Río de la Plata)
        var baseLat = -34.8920 - ((h % 180) * 0.00011);
        var baseLng = -56.1850 + (((h >> 3) % 420) * 0.00014);
        return { lat: baseLat, lng: baseLng };
    }

    function escaparHtml(texto) {
        if (texto === null || texto === undefined) return '';
        return String(texto)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function destruirMapaSiExiste(idContenedor) {
        var mapaPrevio = instanciasMapas[idContenedor];
        if (mapaPrevio) {
            try {
                mapaPrevio.remove();
            } catch (e) {
                // Ignorar si el nodo ya fue reemplazado
            }
            delete instanciasMapas[idContenedor];
            delete marcadoresPorContenedor[idContenedor];
        }
    }

    function crearMapaBase(idContenedor, centroLat, centroLng, zoomInicial) {
        if (typeof L === 'undefined') {
            console.warn('Leaflet no está disponible aún en la página.');
            return null;
        }

        var contenedor = document.getElementById(idContenedor);
        if (!contenedor) {
            return null;
        }

        destruirMapaSiExiste(idContenedor);

        var mapa = L.map(contenedor, {
            center: [centroLat, centroLng],
            zoom: zoomInicial || 13,
            zoomControl: true,
            attributionControl: true
        });

        // Capa base CartoDB Positron adaptada a la paleta sobria de Última Milla
        var capaPositron = L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
            subdomains: 'abcd',
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap &copy; CARTO'
        });

        capaPositron.addTo(mapa);

        instanciasMapas[idContenedor] = mapa;
        marcadoresPorContenedor[idContenedor] = {};

        // Asegurar recálculo de dimensiones tras renderizado en pestañas o paneles dinámicos
        setTimeout(function () {
            if (instanciasMapas[idContenedor]) {
                instanciasMapas[idContenedor].invalidateSize();
            }
        }, 180);

        return mapa;
    }

    function crearIconoDeposito() {
        var html = [
            '<div class="um-marker-deposito" title="Depósito Logístico Central">',
            '  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#ffffff" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">',
            '    <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path>',
            '    <polyline points="9 22 9 12 15 12 15 22"></polyline>',
            '  </svg>',
            '</div>'
        ].join('');

        return L.divIcon({
            className: 'um-div-icon',
            html: html,
            iconSize: [32, 32],
            iconAnchor: [16, 16],
            popupAnchor: [0, -16]
        });
    }

    function crearIconoParada(numeroTexto, varianteColor, esDestacada) {
        var tamano = esDestacada ? 30 : 24;
        var mitad = Math.round(tamano / 2);
        var claseColor = 'um-parada-' + (varianteColor || 'pendiente');
        var claseDestacada = esDestacada ? ' um-parada-destacada' : '';

        var html = '<div class="um-marker-parada ' + claseColor + claseDestacada + '" style="width:' + tamano + 'px;height:' + tamano + 'px;">' +
            '<span>' + escaparHtml(numeroTexto) + '</span>' +
            '</div>';

        return L.divIcon({
            className: 'um-div-icon',
            html: html,
            iconSize: [tamano, tamano],
            iconAnchor: [mitad, mitad],
            popupAnchor: [0, -mitad]
        });
    }

    function crearIconoCamion(etiquetaTexto, varianteEstado, seleccionado) {
        var claseEstado = 'um-camion-' + (varianteEstado || 'ruta');
        var claseSel = seleccionado ? ' um-camion-seleccionado' : '';

        var html = [
            '<div class="um-marker-camion-wrap' + claseSel + '">',
            '  <div class="um-marker-camion-etiqueta">' + escaparHtml(etiquetaTexto) + '</div>',
            '  <div class="um-marker-camion-circulo ' + claseEstado + '">',
            '    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="#ffffff" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">',
            '      <rect x="1" y="3" width="15" height="13"></rect>',
            '      <polygon points="16 8 20 8 23 11 23 16 16 16 16 8"></polygon>',
            '      <circle cx="5.5" cy="18.5" r="2.5"></circle>',
            '      <circle cx="18.5" cy="18.5" r="2.5"></circle>',
            '    </svg>',
            '  </div>',
            '</div>'
        ].join('');

        return L.divIcon({
            className: 'um-div-icon',
            html: html,
            iconSize: [86, 52],
            iconAnchor: [43, 34],
            popupAnchor: [0, -30]
        });
    }

    function normalizarEstadoEnvio(estado) {
        if (estado === null || estado === undefined) return 'Admitido';
        var texto = String(estado);
        switch (texto) {
            case '1': return 'Admitido';
            case '2': return 'EnDeposito';
            case '3': return 'AsignadoARuta';
            case '4': return 'EnTransito';
            case '5': return 'Entregado';
            case '6': return 'NoEntregado';
            case '7': return 'Reprogramado';
            case '8': return 'EnDevolucion';
            case '9': return 'Devuelto';
            case '10': return 'Extraviado';
            default: return texto;
        }
    }

    function obtenerVarianteColorPorEstado(estadoNorm, enRiesgo) {
        if (enRiesgo) return 'fallo';
        if (estadoNorm === 'Entregado') return 'entregado';
        if (estadoNorm === 'NoEntregado' || estadoNorm === 'Extraviado') return 'fallo';
        if (estadoNorm === 'EnTransito' || estadoNorm === 'EnCurso' || estadoNorm === 'AsignadoARuta') return 'activo';
        return 'pendiente';
    }

    function obtenerEtiquetaLegibleEstado(estadoNorm) {
        switch (estadoNorm) {
            case 'Admitido': return 'Admitido';
            case 'EnDeposito': return 'En depósito';
            case 'AsignadoARuta': return 'Asignado a ruta';
            case 'EnTransito': return 'En tránsito';
            case 'EnCurso': return 'En curso';
            case 'Entregado': return 'Entregado';
            case 'NoEntregado': return 'No entregado';
            case 'Reprogramado': return 'Reprogramado';
            case 'EnDevolucion': return 'En devolución';
            case 'Devuelto': return 'Devuelto';
            case 'Extraviado': return 'Extraviado';
            default: return estadoNorm;
        }
    }

    /**
     * 1. Renderiza el mapa de seguimiento individual de un envío (Portal del Comercio UC-28 y Backoffice Trazabilidad).
     */
    function renderizarMapaSeguimientoEnvio(idContenedor, envio) {
        if (!envio) return;

        var codigo = envio.codigoSeguimiento || envio.CodigoSeguimiento || 'ENV';
        var destinatario = envio.nombreDestinatario || envio.NombreDestinatario || 'Destinatario';
        var direccion = envio.direccionDestino || envio.DireccionDestino || 'Montevideo';
        var estadoNorm = normalizarEstadoEnvio(envio.estado !== undefined ? envio.estado : envio.Estado);
        var numeroRuta = envio.hojaDeRutaNumero || envio.HojaDeRutaNumero || null;
        var repartidor = envio.repartidorNombre || envio.RepartidorNombre || null;

        var coordDestino = (envio.latitud && envio.longitud)
            ? { lat: envio.latitud, lng: envio.longitud }
            : resolverCoordenadasMontevideo(direccion, '', codigo);

        var mapa = crearMapaBase(idContenedor, coordDestino.lat, coordDestino.lng, 13);
        if (!mapa) return;

        var puntosAjuste = [
            [DEPOSITO_CENTRAL.lat, DEPOSITO_CENTRAL.lng],
            [coordDestino.lat, coordDestino.lng]
        ];

        // 1. Marcador del Depósito Central
        var markerDeposito = L.marker([DEPOSITO_CENTRAL.lat, DEPOSITO_CENTRAL.lng], {
            icon: crearIconoDeposito()
        }).addTo(mapa);

        markerDeposito.bindPopup(
            '<div class="um-popup">' +
            '  <div class="um-popup-titulo">' + escaparHtml(DEPOSITO_CENTRAL.nombre) + '</div>' +
            '  <div class="um-popup-sub">' + escaparHtml(DEPOSITO_CENTRAL.direccion) + '</div>' +
            '  <div class="um-popup-meta">Centro de admisión, clasificación y despacho</div>' +
            '</div>'
        );

        var tieneCamionActivo = (estadoNorm === 'EnTransito' || estadoNorm === 'AsignadoARuta');

        // 2. Si el envío está en ruta / tránsito, dibujamos paradas previas de la hoja de ruta y el camión de reparto
        if (tieneCamionActivo) {
            var paradaIntermedia1 = {
                lat: DEPOSITO_CENTRAL.lat + (coordDestino.lat - DEPOSITO_CENTRAL.lat) * 0.35 + 0.0032,
                lng: DEPOSITO_CENTRAL.lng + (coordDestino.lng - DEPOSITO_CENTRAL.lng) * 0.35 - 0.0025
            };
            var paradaIntermedia2 = {
                lat: DEPOSITO_CENTRAL.lat + (coordDestino.lat - DEPOSITO_CENTRAL.lat) * 0.62 - 0.0020,
                lng: DEPOSITO_CENTRAL.lng + (coordDestino.lng - DEPOSITO_CENTRAL.lng) * 0.62 + 0.0028
            };

            var coordCamion = estadoNorm === 'EnTransito'
                ? {
                    lat: paradaIntermedia2.lat + (coordDestino.lat - paradaIntermedia2.lat) * 0.45,
                    lng: paradaIntermedia2.lng + (coordDestino.lng - paradaIntermedia2.lng) * 0.45
                }
                : {
                    lat: DEPOSITO_CENTRAL.lat + (paradaIntermedia1.lat - DEPOSITO_CENTRAL.lat) * 0.25,
                    lng: DEPOSITO_CENTRAL.lng + (paradaIntermedia1.lng - DEPOSITO_CENTRAL.lng) * 0.25
                };

            var markerP1 = L.marker([paradaIntermedia1.lat, paradaIntermedia1.lng], {
                icon: crearIconoParada('1', 'entregado', false)
            }).addTo(mapa);
            markerP1.bindPopup(
                '<div class="um-popup">' +
                '  <div class="um-popup-titulo">Parada #1 · Hoja ' + escaparHtml(numeroRuta || 'HDR-2026-0081') + '</div>' +
                '  <div class="um-popup-meta">Estado: Entregado previamente en el recorrido</div>' +
                '</div>'
            );

            var markerP2 = L.marker([paradaIntermedia2.lat, paradaIntermedia2.lng], {
                icon: crearIconoParada('2', estadoNorm === 'EnTransito' ? 'entregado' : 'pendiente', false)
            }).addTo(mapa);
            markerP2.bindPopup(
                '<div class="um-popup">' +
                '  <div class="um-popup-titulo">Parada #2 · Hoja ' + escaparHtml(numeroRuta || 'HDR-2026-0081') + '</div>' +
                '  <div class="um-popup-meta">Estado: ' + (estadoNorm === 'EnTransito' ? 'Completada' : 'Programada en ruta') + '</div>' +
                '</div>'
            );

            L.polyline([
                [DEPOSITO_CENTRAL.lat, DEPOSITO_CENTRAL.lng],
                [paradaIntermedia1.lat, paradaIntermedia1.lng],
                [paradaIntermedia2.lat, paradaIntermedia2.lng],
                [coordCamion.lat, coordCamion.lng]
            ], {
                color: '#1D4ED8',
                weight: 3.5,
                opacity: 0.85
            }).addTo(mapa);

            L.polyline([
                [coordCamion.lat, coordCamion.lng],
                [coordDestino.lat, coordDestino.lng]
            ], {
                color: '#1D4ED8',
                weight: 3,
                dashArray: '6, 6',
                opacity: 0.75
            }).addTo(mapa);

            var etiquetaCamion = numeroRuta || 'En Reparto';
            var markerCamion = L.marker([coordCamion.lat, coordCamion.lng], {
                icon: crearIconoCamion(etiquetaCamion, estadoNorm === 'EnTransito' ? 'movimiento' : 'ruta', true),
                zIndexOffset: 500
            }).addTo(mapa);

            markerCamion.bindPopup(
                '<div class="um-popup">' +
                '  <div class="um-popup-titulo">Unidad de Reparto · ' + escaparHtml(etiquetaCamion) + '</div>' +
                '  <div class="um-popup-sub">Repartidor: ' + escaparHtml(repartidor || 'Juan Pérez') + '</div>' +
                '  <div class="um-popup-meta">Rumbo a parada de entrega (' + escaparHtml(codigo) + ')</div>' +
                '</div>'
            );

            puntosAjuste.push([paradaIntermedia1.lat, paradaIntermedia1.lng]);
            puntosAjuste.push([paradaIntermedia2.lat, paradaIntermedia2.lng]);
            puntosAjuste.push([coordCamion.lat, coordCamion.lng]);
        } else {
            var colorLinea = estadoNorm === 'Entregado'
                ? '#15803D'
                : (estadoNorm === 'NoEntregado' || estadoNorm === 'Extraviado' ? '#B91C1C' : '#64748B');

            L.polyline([
                [DEPOSITO_CENTRAL.lat, DEPOSITO_CENTRAL.lng],
                [coordDestino.lat, coordDestino.lng]
            ], {
                color: colorLinea,
                weight: 2.8,
                dashArray: estadoNorm === 'Entregado' ? null : '6, 6',
                opacity: 0.75
            }).addTo(mapa);
        }

        // 3. Marcador de la Parada de Destino del Envío
        var varianteDestino = obtenerVarianteColorPorEstado(estadoNorm, false);
        var etiquetaParada = tieneCamionActivo ? '3' : 'P';
        var markerDestino = L.marker([coordDestino.lat, coordDestino.lng], {
            icon: crearIconoParada(etiquetaParada, varianteDestino, true),
            zIndexOffset: 400
        }).addTo(mapa);

        var htmlPopupDestino = [
            '<div class="um-popup">',
            '  <div class="um-popup-titulo">Parada de Entrega · ' + escaparHtml(codigo) + '</div>',
            '  <div class="um-popup-sub">' + escaparHtml(destinatario) + '</div>',
            '  <div class="um-popup-meta">' + escaparHtml(direccion) + '</div>',
            '  <div class="um-popup-estado">Estado: <strong>' + escaparHtml(obtenerEtiquetaLegibleEstado(estadoNorm)) + '</strong></div>',
            '</div>'
        ].join('');

        markerDestino.bindPopup(htmlPopupDestino);

        mapa.fitBounds(puntosAjuste, { padding: [38, 38], maxZoom: 15 });
    }

    /**
     * 2. Renderiza el mapa táctico de toda la flota en el Tablero de Operaciones en Vivo (Backoffice UC-12).
     */
    function renderizarMapaTableroFlota(idContenedor, vehiculos, callbackSeleccion) {
        // En cada actualización en vivo el mapa se redibuja: se conserva la vista que dejó el usuario (zoom y desplazamiento)
        // para que no vuelva al encuadre general cada pocos segundos.
        // Solo se respeta si el usuario movió o acercó el mapa; si no, cada actualización vuelve a encuadrar la flota.
        var vistaPrevia = null;
        var mapaPrevio = instanciasMapas[idContenedor];
        if (mapaPrevio && vistaModificadaPorUsuario[idContenedor]) {
            try {
                vistaPrevia = { centro: mapaPrevio.getCenter(), zoom: mapaPrevio.getZoom() };
            } catch (e) {
                vistaPrevia = null;
            }
        }

        var mapa = crearMapaBase(idContenedor, -34.8960, -56.1620, 12);
        if (!mapa) return;

        ['mousedown', 'wheel', 'touchstart', 'dblclick'].forEach(function (evento) {
            mapa.getContainer().addEventListener(evento, function () {
                vistaModificadaPorUsuario[idContenedor] = true;
            }, { passive: true });
        });

        var listaVehiculos = Array.isArray(vehiculos) ? vehiculos : [];
        var puntosTotales = [[DEPOSITO_CENTRAL.lat, DEPOSITO_CENTRAL.lng]];
        var registroContenedor = {
            vehiculos: {},
            puntosGlobales: puntosTotales,
            callbackSeleccion: callbackSeleccion
        };
        marcadoresPorContenedor[idContenedor] = registroContenedor;

        var markerDep = L.marker([DEPOSITO_CENTRAL.lat, DEPOSITO_CENTRAL.lng], {
            icon: crearIconoDeposito()
        }).addTo(mapa);
        markerDep.bindPopup(
            '<div class="um-popup">' +
            '  <div class="um-popup-titulo">' + escaparHtml(DEPOSITO_CENTRAL.nombre) + '</div>' +
            '  <div class="um-popup-sub">' + escaparHtml(DEPOSITO_CENTRAL.direccion) + '</div>' +
            '</div>'
        );

        listaVehiculos.forEach(function (v, indiceVeh) {
            var matricula = v.vehiculoMatricula || v.VehiculoMatricula || ('UNIDAD-' + (indiceVeh + 1));
            var modelo = v.vehiculoModelo || v.VehiculoModelo || '';
            var numeroRuta = v.numeroRuta || v.NumeroRuta || '';
            var repartidor = v.repartidorNombre || v.RepartidorNombre || '';
            var direccionAprox = v.direccionAproximada || v.DireccionAproximada || '';
            var estadoOp = v.estadoOperativo || v.EstadoOperativo || 'EnMovimiento';
            var tieneRiesgo = Boolean(v.tieneAlertasRiesgo !== undefined ? v.tieneAlertasRiesgo : v.TieneAlertasRiesgo);
            var velocidad = v.velocidadKmh !== undefined ? v.velocidadKmh : (v.VelocidadKmh || 0);
            var paradas = v.paradas || v.Paradas || [];

            var latVeh = v.latitud !== undefined ? v.latitud : v.Latitud;
            var lngVeh = v.longitud !== undefined ? v.longitud : v.Longitud;
            if (!latVeh || !lngVeh) {
                var coordRes = resolverCoordenadasMontevideo(direccionAprox, v.zonaActual || v.ZonaActual, matricula);
                latVeh = coordRes.lat;
                lngVeh = coordRes.lng;
            }

            puntosTotales.push([latVeh, lngVeh]);
            var puntosRutaVehiculo = [[latVeh, lngVeh]];
            var coordenadasPolilinea = [];

            paradas.forEach(function (par, idxPar) {
                var orden = par.orden || par.Orden || (idxPar + 1);
                var codEnvio = par.codigoSeguimiento || par.CodigoSeguimiento || '';
                var destPar = par.destinatario || par.Destinatario || '';
                var dirPar = par.direccion || par.Direccion || '';
                var franjaPar = par.franjaHoraria || par.FranjaHoraria || '';
                var estadoPar = normalizarEstadoEnvio(par.estado || par.Estado || 'Pendiente');
                var enRiesgoPar = Boolean(par.enRiesgo !== undefined ? par.enRiesgo : par.EnRiesgo);
                var motivoRiesgo = par.motivoRiesgo || par.MotivoRiesgo || '';

                var latPar = par.latitud !== undefined ? par.latitud : par.Latitud;
                var lngPar = par.longitud !== undefined ? par.longitud : par.Longitud;
                if (!latPar || !lngPar) {
                    var cPar = resolverCoordenadasMontevideo(dirPar, '', codEnvio + '-' + orden);
                    latPar = cPar.lat;
                    lngPar = cPar.lng;
                }

                coordenadasPolilinea.push([latPar, lngPar]);
                puntosTotales.push([latPar, lngPar]);
                puntosRutaVehiculo.push([latPar, lngPar]);

                var varianteParada = obtenerVarianteColorPorEstado(estadoPar, enRiesgoPar);
                var markerParada = L.marker([latPar, lngPar], {
                    icon: crearIconoParada(String(orden), varianteParada, enRiesgoPar || estadoPar === 'EnCurso')
                }).addTo(mapa);

                var popupParada = [
                    '<div class="um-popup">',
                    '  <div class="um-popup-titulo">Parada #' + orden + ' · ' + escaparHtml(codEnvio) + '</div>',
                    '  <div class="um-popup-sub">' + escaparHtml(destPar) + '</div>',
                    '  <div class="um-popup-meta">' + escaparHtml(dirPar) + '</div>',
                    franjaPar ? ('  <div class="um-popup-meta">Ventana: ' + escaparHtml(franjaPar) + '</div>') : '',
                    '  <div class="um-popup-estado">Unidad: <strong>' + escaparHtml(matricula) + '</strong> (' + escaparHtml(obtenerEtiquetaLegibleEstado(estadoPar)) + ')</div>',
                    (enRiesgoPar && motivoRiesgo) ? ('  <div class="um-popup-alerta">Alerta SLA: ' + escaparHtml(motivoRiesgo) + '</div>') : '',
                    '</div>'
                ].join('');

                markerParada.bindPopup(popupParada);
                markerParada.on('click', function () {
                    if (typeof callbackSeleccion === 'function') {
                        callbackSeleccion(v);
                    }
                });
            });

            if (coordenadasPolilinea.length > 0) {
                var colorRuta = tieneRiesgo ? '#B91C1C' : (estadoOp === 'EnMovimiento' ? '#1D4ED8' : '#64748B');
                L.polyline(coordenadasPolilinea, {
                    color: colorRuta,
                    weight: 2.6,
                    dashArray: '5, 6',
                    opacity: 0.65
                }).addTo(mapa);
            }

            var varianteCamion = tieneRiesgo
                ? 'riesgo'
                : (estadoOp === 'EnMovimiento' ? 'movimiento' : 'detenido');

            var markerCamion = L.marker([latVeh, lngVeh], {
                icon: crearIconoCamion(matricula, varianteCamion, indiceVeh === 0),
                zIndexOffset: 600
            }).addTo(mapa);

            var popupCamion = [
                '<div class="um-popup">',
                '  <div class="um-popup-titulo">' + escaparHtml(matricula) + ' · ' + escaparHtml(numeroRuta) + '</div>',
                '  <div class="um-popup-sub">Chofer: ' + escaparHtml(repartidor) + ' (' + escaparHtml(modelo) + ')</div>',
                '  <div class="um-popup-meta">' + escaparHtml(direccionAprox) + ' · ' + velocidad + ' km/h</div>',
                '</div>'
            ].join('');

            markerCamion.bindPopup(popupCamion);
            markerCamion.on('click', function () {
                resaltarCamionSeleccionado(idContenedor, matricula);
                if (typeof callbackSeleccion === 'function') {
                    callbackSeleccion(v);
                }
            });

            registroContenedor.vehiculos[matricula] = {
                datos: v,
                marker: markerCamion,
                variante: varianteCamion,
                puntosRuta: puntosRutaVehiculo
            };
        });

        if (vistaPrevia) {
            mapa.setView(vistaPrevia.centro, vistaPrevia.zoom, { animate: false });
        } else if (puntosTotales.length > 1) {
            mapa.fitBounds(puntosTotales, { padding: [36, 36], maxZoom: 14 });
        }
    }

    function resaltarCamionSeleccionado(idContenedor, matriculaSeleccionada) {
        var reg = marcadoresPorContenedor[idContenedor];
        if (!reg || !reg.vehiculos) return;

        Object.keys(reg.vehiculos).forEach(function (mat) {
            var item = reg.vehiculos[mat];
            var esSel = (mat === matriculaSeleccionada);
            item.marker.setIcon(crearIconoCamion(mat, item.variante, esSel));
        });
    }

    function enfocarVehiculoEnTablero(idContenedor, matricula, abrirPopup) {
        var mapa = instanciasMapas[idContenedor];
        var reg = marcadoresPorContenedor[idContenedor];
        if (!mapa || !reg || !reg.vehiculos || !reg.vehiculos[matricula]) return;

        mapa.invalidateSize();
        resaltarCamionSeleccionado(idContenedor, matricula);

        var item = reg.vehiculos[matricula];
        if (item.puntosRuta && item.puntosRuta.length > 1) {
            mapa.fitBounds(item.puntosRuta, { padding: [42, 42], maxZoom: 15 });
        } else {
            mapa.setView(item.marker.getLatLng(), 15);
        }

        if (abrirPopup) {
            item.marker.openPopup();
        }
    }

    function ajustarVistaGeneralMapa(idContenedor) {
        vistaModificadaPorUsuario[idContenedor] = false;
        var mapa = instanciasMapas[idContenedor];
        var reg = marcadoresPorContenedor[idContenedor];
        if (!mapa) return;

        mapa.invalidateSize();
        if (reg && reg.puntosGlobales && reg.puntosGlobales.length > 1) {
            mapa.fitBounds(reg.puntosGlobales, { padding: [36, 36], maxZoom: 14 });
        }
    }

    /**
     * 3. Renderiza el mapa de una Hoja de Ruta con sus paradas numeradas y su camión de reparto (Backoffice Detalle de Ruta).
     */
    function renderizarMapaDetalleRuta(idContenedor, hojaDeRuta) {
        if (!hojaDeRuta) return;

        var mapa = crearMapaBase(idContenedor, DEPOSITO_CENTRAL.lat, DEPOSITO_CENTRAL.lng, 13);
        if (!mapa) return;

        var numeroRuta = hojaDeRuta.numeroRuta || hojaDeRuta.NumeroRuta || 'Hoja de Ruta';
        var estadoRuta = hojaDeRuta.estado || hojaDeRuta.Estado || 'Liberada';
        var matricula = hojaDeRuta.vehiculoMatricula || hojaDeRuta.VehiculoMatricula || 'Unidad';
        var modelo = hojaDeRuta.vehiculoModelo || hojaDeRuta.VehiculoModelo || '';
        var repartidor = hojaDeRuta.repartidorNombre || hojaDeRuta.RepartidorNombre || 'Repartidor';
        var paradas = hojaDeRuta.paradas || hojaDeRuta.Paradas || [];

        var puntosPolilinea = [[DEPOSITO_CENTRAL.lat, DEPOSITO_CENTRAL.lng]];
        var dicParadas = {};

        var markerDep = L.marker([DEPOSITO_CENTRAL.lat, DEPOSITO_CENTRAL.lng], {
            icon: crearIconoDeposito()
        }).addTo(mapa);
        markerDep.bindPopup(
            '<div class="um-popup">' +
            '  <div class="um-popup-titulo">Salida: ' + escaparHtml(DEPOSITO_CENTRAL.nombre) + '</div>' +
            '  <div class="um-popup-sub">' + escaparHtml(DEPOSITO_CENTRAL.direccion) + '</div>' +
            '  <div class="um-popup-meta">Inicio del recorrido ' + escaparHtml(numeroRuta) + '</div>' +
            '</div>'
        );

        var ultimaEntregadaCoord = null;
        var primeraPendienteCoord = null;

        paradas.forEach(function (p, idx) {
            var orden = p.orden || p.Orden || (idx + 1);
            var codigo = p.codigoSeguimiento || p.CodigoSeguimiento || '';
            var destinatario = p.destinatario || p.Destinatario || '';
            var direccion = p.direccion || p.Direccion || '';
            var zona = p.zona || p.Zona || '';
            var franja = p.franjaHoraria || p.FranjaHoraria || '';
            var estadoPar = normalizarEstadoEnvio(p.estado || p.Estado || 'Pendiente');

            var coord = (p.latitud && p.longitud)
                ? { lat: p.latitud, lng: p.longitud }
                : resolverCoordenadasMontevideo(direccion, zona, codigo + '-' + orden);

            puntosPolilinea.push([coord.lat, coord.lng]);

            if (estadoPar === 'Entregado') {
                ultimaEntregadaCoord = coord;
            } else if (!primeraPendienteCoord) {
                primeraPendienteCoord = coord;
            }

            var variante = obtenerVarianteColorPorEstado(estadoPar, false);
            var markerParada = L.marker([coord.lat, coord.lng], {
                icon: crearIconoParada(String(orden), variante, estadoPar === 'EnTransito' || estadoPar === 'EnCurso')
            }).addTo(mapa);

            var popupHtml = [
                '<div class="um-popup">',
                '  <div class="um-popup-titulo">Parada #' + orden + ' · ' + escaparHtml(codigo) + '</div>',
                '  <div class="um-popup-sub">' + escaparHtml(destinatario) + '</div>',
                '  <div class="um-popup-meta">' + escaparHtml(direccion) + (zona ? ' (' + escaparHtml(zona) + ')' : '') + '</div>',
                franja ? ('  <div class="um-popup-meta">Franja: ' + escaparHtml(franja) + '</div>') : '',
                '  <div class="um-popup-estado">Estado: <strong>' + escaparHtml(obtenerEtiquetaLegibleEstado(estadoPar)) + '</strong></div>',
                '</div>'
            ].join('');

            markerParada.bindPopup(popupHtml);
            dicParadas[orden] = markerParada;
        });

        if (puntosPolilinea.length > 1) {
            var enCalle = (estadoRuta === 'Despachada' || estadoRuta === 'EnTransito');
            L.polyline(puntosPolilinea, {
                color: enCalle ? '#1D4ED8' : '#475569',
                weight: 3,
                dashArray: enCalle ? null : '6, 6',
                opacity: 0.78
            }).addTo(mapa);
        }

        if (estadoRuta !== 'Finalizada' && estadoRuta !== 'Cancelada') {
            var enCirculacion = (estadoRuta === 'Despachada' || estadoRuta === 'EnTransito');
            var coordCamion;

            if (enCirculacion && primeraPendienteCoord) {
                var origenTramo = ultimaEntregadaCoord || DEPOSITO_CENTRAL;
                coordCamion = {
                    lat: origenTramo.lat + (primeraPendienteCoord.lat - origenTramo.lat) * 0.55,
                    lng: origenTramo.lng + (primeraPendienteCoord.lng - origenTramo.lng) * 0.55
                };
            } else if (enCirculacion && puntosPolilinea.length > 1) {
                coordCamion = {
                    lat: puntosPolilinea[1][0],
                    lng: puntosPolilinea[1][1]
                };
            } else {
                coordCamion = {
                    lat: DEPOSITO_CENTRAL.lat + 0.0012,
                    lng: DEPOSITO_CENTRAL.lng + 0.0015
                };
            }

            var markerCamion = L.marker([coordCamion.lat, coordCamion.lng], {
                icon: crearIconoCamion(matricula, enCirculacion ? 'movimiento' : 'ruta', true),
                zIndexOffset: 600
            }).addTo(mapa);

            markerCamion.bindPopup(
                '<div class="um-popup">' +
                '  <div class="um-popup-titulo">' + escaparHtml(matricula) + ' · ' + escaparHtml(numeroRuta) + '</div>' +
                '  <div class="um-popup-sub">Repartidor: ' + escaparHtml(repartidor) + (modelo ? ' (' + escaparHtml(modelo) + ')' : '') + '</div>' +
                '  <div class="um-popup-meta">Estado de ruta: <strong>' + escaparHtml(estadoRuta) + '</strong></div>' +
                '</div>'
            );

            puntosPolilinea.push([coordCamion.lat, coordCamion.lng]);
        }

        marcadoresPorContenedor[idContenedor] = {
            paradas: dicParadas,
            puntosGlobales: puntosPolilinea
        };

        if (puntosPolilinea.length > 1) {
            mapa.fitBounds(puntosPolilinea, { padding: [38, 38], maxZoom: 15 });
        }
    }

    function enfocarParadaEnRuta(idContenedor, ordenParada) {
        var mapa = instanciasMapas[idContenedor];
        var reg = marcadoresPorContenedor[idContenedor];
        if (!mapa || !reg || !reg.paradas || !reg.paradas[ordenParada]) return;

        var marker = reg.paradas[ordenParada];
        mapa.invalidateSize();
        mapa.setView(marker.getLatLng(), 16, { animate: true });
        marker.openPopup();
    }

    window.MapasUltimaMilla = {
        renderizarMapaSeguimientoEnvio: renderizarMapaSeguimientoEnvio,
        renderizarMapaTableroFlota: renderizarMapaTableroFlota,
        enfocarVehiculoEnTablero: enfocarVehiculoEnTablero,
        ajustarVistaGeneralMapa: ajustarVistaGeneralMapa,
        renderizarMapaDetalleRuta: renderizarMapaDetalleRuta,
        enfocarParadaEnRuta: enfocarParadaEnRuta
    };
})(window);

