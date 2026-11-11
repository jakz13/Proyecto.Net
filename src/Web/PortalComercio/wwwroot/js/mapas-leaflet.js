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

    // Catálogo de esquinas e intersecciones logísticas de Montevideo y Canelones
    var CATALOGO_INTERSECCIONES = [
        { clave: '18 de julio y ejido', nombre: '18 de Julio y Ejido (Centro)', lat: -34.9056, lng: -56.1866 },
        { clave: '18 de julio y rio negro', nombre: '18 de Julio y Río Negro (Centro)', lat: -34.9058, lng: -56.1925 },
        { clave: '18 de julio y plaza independencia', nombre: '18 de Julio y Plaza Independencia', lat: -34.9064, lng: -56.1995 },
        { clave: '18 de julio y bvar. artigas', nombre: '18 de Julio y Bvar. Artigas (Tres Cruces)', lat: -34.8967, lng: -56.1664 },
        { clave: '18 de julio y bulevar artigas', nombre: '18 de Julio y Bvar. Artigas (Tres Cruces)', lat: -34.8967, lng: -56.1664 },
        { clave: '18 de julio y minas', nombre: '18 de Julio y Minas (Cordón)', lat: -34.9048, lng: -56.1788 },
        { clave: '18 de julio y andes', nombre: '18 de Julio y Andes (Centro)', lat: -34.9062, lng: -56.1970 },
        { clave: 'rivera y bvar. artigas', nombre: 'Av. Rivera y Bvar. Artigas (Pocitos)', lat: -34.9058, lng: -56.1648 },
        { clave: 'rivera y bulevar artigas', nombre: 'Av. Rivera y Bvar. Artigas (Pocitos)', lat: -34.9058, lng: -56.1648 },
        { clave: 'rivera y soca', nombre: 'Av. Rivera y Soca (Pocitos)', lat: -34.9038, lng: -56.1554 },
        { clave: 'rivera y luis alberto de herrera', nombre: 'Av. Rivera y L. A. de Herrera (Pocitos Nuevo)', lat: -34.8998, lng: -56.1360 },
        { clave: 'rivera y comercio', nombre: 'Av. Rivera y Comercio (Buceo)', lat: -34.8985, lng: -56.1285 },
        { clave: 'rivera y propios', nombre: 'Av. Rivera y Propios (Buceo)', lat: -34.8988, lng: -56.1305 },
        { clave: 'bvar. artigas y garibaldi', nombre: 'Bvar. Artigas y Garibaldi', lat: -34.8872, lng: -56.1685 },
        { clave: 'bvar. artigas y gral. flores', nombre: 'Bvar. Artigas y Gral. Flores (Goes)', lat: -34.8722, lng: -56.1755 },
        { clave: 'bvar. artigas y suarez', nombre: 'Bvar. Artigas y Joaquín Suárez (Prado)', lat: -34.8640, lng: -56.1985 },
        { clave: 'bvar. artigas y españa', nombre: 'Bvar. Artigas y Bvar. España (Parque Rodó)', lat: -34.9125, lng: -56.1610 },
        { clave: 'av. brasil y benito blanco', nombre: 'Av. Brasil y Benito Blanco (Pocitos)', lat: -34.9152, lng: -56.1465 },
        { clave: 'av. brasil y 26 de marzo', nombre: 'Av. Brasil y 26 de Marzo (Pocitos)', lat: -34.9142, lng: -56.1488 },
        { clave: 'av. brasil y libertad', nombre: 'Av. Brasil y Libertad (Pocitos)', lat: -34.9135, lng: -56.1528 },
        { clave: '21 de setiembre y ellauri', nombre: '21 de Setiembre y Ellauri (Punta Carretas)', lat: -34.9192, lng: -56.1568 },
        { clave: '21 de setiembre y bvar. españa', nombre: '21 de Setiembre y Bvar. España', lat: -34.9138, lng: -56.1550 },
        { clave: 'agraciada y suarez', nombre: 'Av. Agraciada y Joaquín Suárez (Bella Vista)', lat: -34.8672, lng: -56.2045 },
        { clave: 'agraciada y asuncion', nombre: 'Av. Agraciada y Asunción (Aguada)', lat: -34.8845, lng: -56.1950 },
        { clave: 'agraciada y san fructuoso', nombre: 'Av. Agraciada y San Fructuoso', lat: -34.8728, lng: -56.2005 },
        { clave: '8 de octubre y garibaldi', nombre: 'Av. 8 de Octubre y Garibaldi (La Blanqueada)', lat: -34.8865, lng: -56.1605 },
        { clave: '8 de octubre y centenario', nombre: 'Av. 8 de Octubre y Av. Centenario', lat: -34.8848, lng: -56.1560 },
        { clave: '8 de octubre y comercio', nombre: 'Av. 8 de Octubre y Comercio (Unión)', lat: -34.8765, lng: -56.1380 },
        { clave: '8 de octubre y propios', nombre: 'Av. 8 de Octubre y Propios (Batlle y Ordóñez)', lat: -34.8780, lng: -56.1430 },
        { clave: '8 de octubre y belloni', nombre: 'Av. 8 de Octubre y Belloni (Maroñas)', lat: -34.8580, lng: -56.1310 },
        { clave: 'av. italia y propios', nombre: 'Av. Italia y Propios (Buena Vista)', lat: -34.8890, lng: -56.1360 },
        { clave: 'av. italia y comercio', nombre: 'Av. Italia y Comercio (Malvín Norte)', lat: -34.8905, lng: -56.1305 },
        { clave: 'av. italia y bolivia', nombre: 'Av. Italia y Bolivia (Carrasco)', lat: -34.8850, lng: -56.0790 },
        { clave: 'av. italia y barradas', nombre: 'Av. Italia y Barradas (Puente Carrasco)', lat: -34.8790, lng: -56.0350 },
        { clave: 'rambla y kibon', nombre: 'Rambla y Kibón (Pocitos)', lat: -34.9110, lng: -56.1350 },
        { clave: 'rambla y punta carretas', nombre: 'Rambla Mahatma Gandhi (Punta Carretas)', lat: -34.9275, lng: -56.1595 },
        { clave: 'rambla y ciudad vieja', nombre: 'Rambla Portuaria y Ciudad Vieja', lat: -34.9090, lng: -56.2090 },
        { clave: 'giannattasio y calcagno', nombre: 'Av. Giannattasio y Calcagno (Ciudad de la Costa)', lat: -34.8420, lng: -55.9960 },
        { clave: 'giannattasio y marquez castro', nombre: 'Av. Giannattasio y Márquez Castro (Solymar)', lat: -34.8360, lng: -55.9780 }
    ];

    function normalizarTextoBusqueda(txt) {
        return String(txt || '').toLowerCase()
            .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
            .replace(/[.,]/g, ' ')
            .replace(/\s+/g, ' ')
            .trim();
    }

    function geocodificarInterseccion(textoEntrada) {
        if (!textoEntrada || typeof textoEntrada !== 'string') return null;
        var norm = normalizarTextoBusqueda(textoEntrada);
        if (!norm) return null;

        for (var i = 0; i < CATALOGO_INTERSECCIONES.length; i++) {
            var item = CATALOGO_INTERSECCIONES[i];
            var claveNorm = normalizarTextoBusqueda(item.clave);
            if (norm === claveNorm || norm.indexOf(claveNorm) !== -1 || claveNorm.indexOf(norm) !== -1) {
                return { lat: item.lat, lng: item.lng, nombre: item.nombre };
            }
        }

        var separadores = [' y ', ' e ', ' con ', ' & ', ' / ', ' - '];
        for (var s = 0; s < separadores.length; s++) {
            if (norm.indexOf(separadores[s]) !== -1) {
                var partes = norm.split(separadores[s]);
                if (partes.length >= 2) {
                    var calle1 = partes[0].trim();
                    var calle2 = partes[1].trim();
                    var coord1 = resolverCoordenadasMontevideo(calle1, '');
                    var coord2 = resolverCoordenadasMontevideo(calle2, '');
                    if (coord1 && coord2) {
                        return {
                            lat: Number(((coord1.lat + coord2.lat) / 2).toFixed(4)),
                            lng: Number(((coord1.lng + coord2.lng) / 2).toFixed(4)),
                            nombre: calle1.toUpperCase() + ' y ' + calle2.toUpperCase()
                        };
                    } else if (coord1) {
                        return { lat: Number(coord1.lat.toFixed(4)), lng: Number(coord1.lng.toFixed(4)), nombre: calle1.toUpperCase() };
                    } else if (coord2) {
                        return { lat: Number(coord2.lat.toFixed(4)), lng: Number(coord2.lng.toFixed(4)), nombre: calle2.toUpperCase() };
                    }
                }
            }
        }

        for (var z = 0; z < CATALOGO_ZONAS.length; z++) {
            var zonaNorm = normalizarTextoBusqueda(CATALOGO_ZONAS[z].clave);
            if (norm.indexOf(zonaNorm) !== -1) {
                return {
                    lat: CATALOGO_ZONAS[z].lat,
                    lng: CATALOGO_ZONAS[z].lng,
                    nombre: CATALOGO_ZONAS[z].clave.toUpperCase()
                };
            }
        }

        for (var d = 0; d < CATALOGO_DIRECCIONES.length; d++) {
            var dirNorm = normalizarTextoBusqueda(CATALOGO_DIRECCIONES[d].clave);
            if (norm.indexOf(dirNorm) !== -1) {
                return {
                    lat: CATALOGO_DIRECCIONES[d].lat,
                    lng: CATALOGO_DIRECCIONES[d].lng,
                    nombre: CATALOGO_DIRECCIONES[d].clave.toUpperCase()
                };
            }
        }

        return null;
    }


    // Caché persistente de geocodificación para mostrar siempre la ubicación real ingresada
    var CLAVE_STORAGE_GEOCACHE = 'um_geocache_v2';
    var cacheGeocodificacion = (function () {
        try {
            var guardado = window.localStorage ? window.localStorage.getItem(CLAVE_STORAGE_GEOCACHE) : null;
            return guardado ? JSON.parse(guardado) : {};
        } catch (e) {
            return {};
        }
    })();

    function guardarEnCacheGeocodificacion(clave, valor) {
        if (!clave || !valor) return;
        cacheGeocodificacion[clave] = valor;
        try {
            if (window.localStorage) {
                window.localStorage.setItem(CLAVE_STORAGE_GEOCACHE, JSON.stringify(cacheGeocodificacion));
            }
        } catch (e) {
            // Ignorar si localStorage está lleno o restringido
        }
    }

    function limpiarDireccionParaGeocodificacion(texto) {
        return String(texto || '')
            .replace(/\bS\/N\b/gi, ' ')
            .replace(/\b(?:apto\.?|apartamento|oficina|of\.?|local|piso|bloque|torre|solar|manzana|mza\.?|padron|padrón)\s*[a-z0-9\-]+/gi, ' ')
            .replace(/\([^\)]*\)/g, ' ')
            .replace(/\bavda\.?\b/gi, 'Avenida')
            .replace(/\bav\.?\b/gi, 'Avenida')
            .replace(/\bbvar\.?\b/gi, 'Bulevar')
            .replace(/\bbv\.?\b/gi, 'Bulevar')
            .replace(/\bgral\.?\b/gi, 'General')
            .replace(/\bcno\.?\b/gi, 'Camino')
            .replace(/\bdr\.?\b/gi, 'Doctor')
            .replace(/\s+,/g, ',')
            .replace(/,+/g, ',')
            .replace(/\s+/g, ' ')
            .trim();
    }

    async function geocodificarEnLineaNominatim(textoEntrada) {
        if (!textoEntrada || typeof textoEntrada !== 'string') return null;
        var claveCache = normalizarTextoBusqueda(textoEntrada);
        if (!claveCache) return null;

        if (cacheGeocodificacion[claveCache]) {
            return cacheGeocodificacion[claveCache];
        }

        var limpia = limpiarDireccionParaGeocodificacion(textoEntrada);
        if (!limpia) return null;

        var consultas = [];
        var consultaPrincipal = /uruguay/i.test(limpia) ? limpia : (limpia + ', Uruguay');
        consultas.push(consultaPrincipal);

        // Si tiene número de puerta y Nominatim no encuentra el número exacto, probar con la calle y ciudad
        var sinNumero = limpia.replace(/\b\d+\b/g, ' ').replace(/\s+/g, ' ').trim();
        if (sinNumero && sinNumero !== limpia) {
            var consultaCalle = /uruguay/i.test(sinNumero) ? sinNumero : (sinNumero + ', Uruguay');
            consultas.push(consultaCalle);
        }

        for (var c = 0; c < consultas.length; c++) {
            try {
                var url = 'https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=uy&q=' + encodeURIComponent(consultas[c]);
                var respuesta = await fetch(url, {
                    headers: { 'Accept': 'application/json' }
                });
                if (respuesta.ok) {
                    var resultados = await respuesta.json();
                    if (resultados && resultados.length > 0) {
                        var primer = resultados[0];
                        var lat = Number(parseFloat(primer.lat).toFixed(5));
                        var lng = Number(parseFloat(primer.lon).toFixed(5));
                        var partes = (primer.display_name || '').split(',');
                        var nombreLimpio = partes[0] ? partes[0].trim() : textoEntrada.trim();
                        var resultadoGeo = {
                            lat: lat,
                            lng: lng,
                            nombre: nombreLimpio
                        };
                        guardarEnCacheGeocodificacion(claveCache, resultadoGeo);
                        return resultadoGeo;
                    }
                }
            } catch (e) {
                // Continúa sin bloquear ante falta de conexión
            }
        }
        return null;
    }

    function encontrarEsquinaCercana(lat, lng) {
        var minD2 = Infinity;
        var mejor = null;
        for (var i = 0; i < CATALOGO_INTERSECCIONES.length; i++) {
            var esq = CATALOGO_INTERSECCIONES[i];
            var dLat = esq.lat - lat;
            var dLng = esq.lng - lng;
            var d2 = dLat * dLat + dLng * dLng;
            if (d2 < minD2) {
                minD2 = d2;
                mejor = esq;
            }
        }
        if (minD2 < 0.0008 && mejor) {
            return mejor.nombre;
        }
        return 'Punto (' + lat.toFixed(4) + ', ' + lng.toFixed(4) + ')';
    }

    function hashTexto(texto) {
        var h = 2166136261;
        var str = String(texto || 'um');
        for (var i = 0; i < str.length; i++) {
            h ^= str.charCodeAt(i);
            h = Math.imul(h, 16777619);
        }
        return Math.abs(h);
    }

    // Ejes viales principales para interpolación instantánea por altura (número de puerta) antes de que responda Nominatim
    var CATALOGO_EJES_VIALES = [
        { claves: ['av. italia', 'avenida italia', 'av italia'], numMin: 2300, numMax: 7500, latIni: -34.8962, lngIni: -56.1655, latFin: -34.8790, lngFin: -56.0350 },
        { claves: ['18 de julio', 'av. 18 de julio', 'avenida 18 de julio'], numMin: 800, numMax: 2400, latIni: -34.9065, lngIni: -56.1995, latFin: -34.8967, lngFin: -56.1664 },
        { claves: ['av. rivera', 'avenida rivera', 'rivera'], numMin: 1900, numMax: 6500, latIni: -34.9022, lngIni: -56.1745, latFin: -34.8895, lngFin: -56.0580 },
        { claves: ['8 de octubre', 'av. 8 de octubre', 'avenida 8 de octubre'], numMin: 2200, numMax: 5000, latIni: -34.8962, lngIni: -56.1660, latFin: -34.8580, lngFin: -56.1310 },
        { claves: ['bvar. artigas', 'bulevar artigas', 'bv. artigas', 'bv artigas'], numMin: 1, numMax: 4200, latIni: -34.9245, lngIni: -56.1618, latFin: -34.8640, lngFin: -56.1985 },
        { claves: ['av. brasil', 'avenida brasil', 'av brasil'], numMin: 2300, numMax: 3200, latIni: -34.9080, lngIni: -56.1638, latFin: -34.9152, lngFin: -56.1460 },
        { claves: ['agraciada', 'av. agraciada', 'avenida agraciada'], numMin: 1400, numMax: 4200, latIni: -34.8950, lngIni: -56.1930, latFin: -34.8595, lngFin: -56.2140 },
        { claves: ['gral. flores', 'general flores', 'av. gral. flores'], numMin: 2000, numMax: 5200, latIni: -34.8885, lngIni: -56.1880, latFin: -34.8440, lngFin: -56.1480 },
        { claves: ['giannattasio', 'av. giannattasio', 'avenida giannattasio'], numMin: 1, numMax: 32000, latIni: -34.8750, lngIni: -56.0280, latFin: -34.7950, lngFin: -55.9050 }
    ];

    function resolverCoordenadasMontevideo(direccion, zona, semillaExtra) {
        var claveCache = normalizarTextoBusqueda(direccion);
        if (claveCache && cacheGeocodificacion[claveCache]) {
            return {
                lat: cacheGeocodificacion[claveCache].lat,
                lng: cacheGeocodificacion[claveCache].lng
            };
        }

        var dirNorm = String(direccion || '').toLowerCase().trim();
        var zonaNorm = String(zona || '').toLowerCase().trim();
        var h = hashTexto(dirNorm + '|' + (semillaExtra || ''));
        var offsetLat = ((h % 140) - 70) * 0.000045;
        var offsetLng = (((h >> 4) % 140) - 70) * 0.000045;

        // Extraer número de puerta si existe en la dirección
        var matchNum = dirNorm.match(/\b(\d{1,5})\b/);
        var numPuerta = matchNum ? parseInt(matchNum[1], 10) : null;

        // 1. Coincidencia exacta en catálogo con número de puerta
        for (var i = 0; i < CATALOGO_DIRECCIONES.length; i++) {
            var itemCat = CATALOGO_DIRECCIONES[i];
            var catTieneNumero = /\d/.test(itemCat.clave);
            if (catTieneNumero && dirNorm.indexOf(itemCat.clave) !== -1) {
                return {
                    lat: itemCat.lat + offsetLat * 0.3,
                    lng: itemCat.lng + offsetLng * 0.3
                };
            }
        }

        // 2. Interpolación sobre ejes viales según número de puerta
        if (numPuerta !== null && !isNaN(numPuerta)) {
            for (var e = 0; e < CATALOGO_EJES_VIALES.length; e++) {
                var eje = CATALOGO_EJES_VIALES[e];
                var coincideEje = false;
                for (var k = 0; k < eje.claves.length; k++) {
                    if (dirNorm.indexOf(eje.claves[k]) !== -1) {
                        coincideEje = true;
                        break;
                    }
                }
                if (coincideEje) {
                    var frac = (numPuerta - eje.numMin) / Math.max(1, (eje.numMax - eje.numMin));
                    frac = Math.max(0, Math.min(1, frac));
                    return {
                        lat: Number((eje.latIni + (eje.latFin - eje.latIni) * frac).toFixed(5)),
                        lng: Number((eje.lngIni + (eje.lngFin - eje.lngIni) * frac).toFixed(5))
                    };
                }
            }
        }

        // 3. Coincidencia general por calle en catálogo
        for (var c = 0; c < CATALOGO_DIRECCIONES.length; c++) {
            if (dirNorm.indexOf(CATALOGO_DIRECCIONES[c].clave) !== -1) {
                return {
                    lat: CATALOGO_DIRECCIONES[c].lat + offsetLat,
                    lng: CATALOGO_DIRECCIONES[c].lng + offsetLng
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

        // Posición segura en el área urbana central de Montevideo mientras resuelve el geocodificador en línea
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
        var capaPositron = L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap</a> contributors'
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

    // Caché en memoria de geometrías OSRM para evitar parpadeos en refrescos en tiempo real
    var cacheRutasOSRM = {};

    /**
     * Obtiene un punto a una fracción (0..1) a lo largo de una polilínea de coordenadas [[lat, lng], ...].
     */
    function interpolarEnPolilinea(puntos, fraccion) {
        if (!puntos || puntos.length === 0) return null;
        if (puntos.length === 1) return { lat: puntos[0][0], lng: puntos[0][1] };
        var f = Math.max(0, Math.min(1, fraccion));
        var distancias = [0];
        var total = 0;
        for (var i = 1; i < puntos.length; i++) {
            var dLat = puntos[i][0] - puntos[i - 1][0];
            var dLng = puntos[i][1] - puntos[i - 1][1];
            total += Math.sqrt(dLat * dLat + dLng * dLng);
            distancias.push(total);
        }
        if (total === 0) return { lat: puntos[0][0], lng: puntos[0][1] };
        var objetivo = total * f;
        for (var j = 1; j < distancias.length; j++) {
            if (distancias[j] >= objetivo) {
                var segInicio = distancias[j - 1];
                var segLongitud = distancias[j] - segInicio;
                var t = segLongitud > 0 ? (objetivo - segInicio) / segLongitud : 0;
                return {
                    lat: puntos[j - 1][0] + (puntos[j][0] - puntos[j - 1][0]) * t,
                    lng: puntos[j - 1][1] + (puntos[j][1] - puntos[j - 1][1]) * t
                };
            }
        }
        var ultimo = puntos[puntos.length - 1];
        return { lat: ultimo[0], lng: ultimo[1] };
    }

    /**
     * Traza una ruta siguiendo calles reales usando la API pública de OSRM.
     * Si OSRM no responde (offline/error), dibuja la polilínea recta como fallback.
     * @param {L.Map} mapa - Instancia del mapa Leaflet
     * @param {Array} puntos - Array de [lat, lng] en orden
     * @param {Object} opciones - Opciones de estilo (color, weight, dashArray, opacity)
     * @param {Function} [alResolverGeometria] - Callback opcional que recibe el array de puntos [lat, lng] de la calle
     * @returns {Promise<L.Polyline>} La capa añadida al mapa
     */
    async function trazarRutaOSRM(mapa, puntos, opciones, alResolverGeometria) {
        if (!puntos || puntos.length < 2) return null;

        var coordenadasUrl = puntos.map(function (p) {
            return p[1].toFixed(5) + ',' + p[0].toFixed(5);
        }).join(';');

        if (cacheRutasOSRM[coordenadasUrl]) {
            var rutaCache = cacheRutasOSRM[coordenadasUrl];
            if (typeof alResolverGeometria === 'function') alResolverGeometria(rutaCache);
            if (mapa && mapa._container) {
                return L.polyline(rutaCache, opciones).addTo(mapa);
            }
            return null;
        }

        var url = 'https://router.project-osrm.org/route/v1/driving/' + coordenadasUrl +
                  '?overview=full&geometries=geojson&steps=false';

        try {
            var respuesta = await fetch(url, { signal: AbortSignal.timeout(6000) });
            if (respuesta.ok) {
                var datos = await respuesta.json();
                if (datos && datos.code === 'Ok' && datos.routes && datos.routes.length > 0) {
                    var coordsGeojson = datos.routes[0].geometry.coordinates;
                    var puntosRuta = coordsGeojson.map(function (c) { return [c[1], c[0]]; });
                    cacheRutasOSRM[coordenadasUrl] = puntosRuta;
                    if (typeof alResolverGeometria === 'function') alResolverGeometria(puntosRuta);
                    if (mapa && mapa._container) {
                        return L.polyline(puntosRuta, opciones).addTo(mapa);
                    }
                    return null;
                }
            }
        } catch (e) {
            // Sin conexión o timeout: fallback silencioso
        }

        if (typeof alResolverGeometria === 'function') alResolverGeometria(puntos);
        if (mapa && mapa._container) {
            return L.polyline(puntos, opciones).addTo(mapa);
        }
        return null;
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

            trazarRutaOSRM(mapa, [
                [DEPOSITO_CENTRAL.lat, DEPOSITO_CENTRAL.lng],
                [paradaIntermedia1.lat, paradaIntermedia1.lng],
                [paradaIntermedia2.lat, paradaIntermedia2.lng],
                [coordCamion.lat, coordCamion.lng]
            ], {
                color: '#1D4ED8',
                weight: 3.5,
                opacity: 0.85
            });

            trazarRutaOSRM(mapa, [
                [coordCamion.lat, coordCamion.lng],
                [coordDestino.lat, coordDestino.lng]
            ], {
                color: '#1D4ED8',
                weight: 3,
                dashArray: '6, 6',
                opacity: 0.75
            });

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

            trazarRutaOSRM(mapa, [
                [DEPOSITO_CENTRAL.lat, DEPOSITO_CENTRAL.lng],
                [coordDestino.lat, coordDestino.lng]
            ], {
                color: colorLinea,
                weight: 2.8,
                dashArray: estadoNorm === 'Entregado' ? null : '6, 6',
                opacity: 0.75
            });
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

        // Si el envío no traía coordenadas explícitas y aún no estaba en caché de geocodificación, resolver dirección real en línea
        var claveCacheEnvio = normalizarTextoBusqueda(direccion);
        if (!(envio.latitud && envio.longitud) && claveCacheEnvio && !cacheGeocodificacion[claveCacheEnvio]) {
            geocodificarEnLineaNominatim(direccion).then(function (geo) {
                if (!geo || instanciasMapas[idContenedor] !== mapa) return;
                renderizarMapaSeguimientoEnvio(idContenedor, Object.assign({}, envio, {
                    latitud: geo.lat,
                    longitud: geo.lng
                }));
            });
        }
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
            var coordenadasPolilinea = [[DEPOSITO_CENTRAL.lat, DEPOSITO_CENTRAL.lng]];

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

            if (coordenadasPolilinea.length > 1) {
                var colorRuta = tieneRiesgo ? '#B91C1C' : (estadoOp === 'EnMovimiento' ? '#1D4ED8' : '#64748B');
                trazarRutaOSRM(mapa, coordenadasPolilinea, {
                    color: colorRuta,
                    weight: 2.6,
                    dashArray: '5, 6',
                    opacity: 0.65
                });
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

        // Geocodificar en segundo plano las paradas de flota cuya dirección aún no esté en caché
        var promesasFlota = [];
        listaVehiculos.forEach(function (v) {
            var paradasVeh = v.paradas || v.Paradas || [];
            paradasVeh.forEach(function (par) {
                var dirPar = par.direccion || par.Direccion || '';
                var clavePar = normalizarTextoBusqueda(dirPar);
                if (!(par.latitud || par.Latitud) && clavePar && !cacheGeocodificacion[clavePar]) {
                    promesasFlota.push(geocodificarEnLineaNominatim(dirPar));
                }
            });
        });
        if (promesasFlota.length > 0) {
            Promise.all(promesasFlota).then(function (resultados) {
                var huboNuevas = resultados.some(function (r) { return r !== null; });
                if (huboNuevas && instanciasMapas[idContenedor] === mapa) {
                    renderizarMapaTableroFlota(idContenedor, vehiculos, callbackSeleccion);
                }
            });
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
            trazarRutaOSRM(mapa, puntosPolilinea, {
                color: enCalle ? '#1D4ED8' : '#475569',
                weight: 3,
                dashArray: enCalle ? null : '6, 6',
                opacity: 0.78
            });
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

            if (enCirculacion && primeraPendienteCoord) {
                var origenRutaCamion = ultimaEntregadaCoord || DEPOSITO_CENTRAL;
                trazarRutaOSRM(null, [
                    [origenRutaCamion.lat, origenRutaCamion.lng],
                    [primeraPendienteCoord.lat, primeraPendienteCoord.lng]
                ], {}, function (geometriaTramo) {
                    var posEnCalle = interpolarEnPolilinea(geometriaTramo, 0.55);
                    if (posEnCalle && markerCamion) {
                        markerCamion.setLatLng([posEnCalle.lat, posEnCalle.lng]);
                    }
                });
            }

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

        // Resolver en línea mediante Nominatim cualquier parada cuya dirección ingresada aún no esté geocodificada en caché
        var promesasParadas = [];
        paradas.forEach(function (p) {
            var direccion = p.direccion || p.Direccion || '';
            var clave = normalizarTextoBusqueda(direccion);
            if (!(p.latitud && p.longitud) && clave && !cacheGeocodificacion[clave]) {
                promesasParadas.push(geocodificarEnLineaNominatim(direccion));
            }
        });

        if (promesasParadas.length > 0) {
            Promise.all(promesasParadas).then(function (resultados) {
                var resueltas = resultados.some(function (r) { return r !== null; });
                if (resueltas && instanciasMapas[idContenedor] === mapa) {
                    renderizarMapaDetalleRuta(idContenedor, hojaDeRuta);
                }
            });
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


    /**
     * Parsea un texto de coordenadas para extraer los vértices del polígono de cobertura.
     * Soporta GeoJSON, matrices numéricas [[lat, lng], ...], pares sueltos por línea o nombres de zonas.
     */
    function parsearCoordenadasPoligono(textoEntrada) {
        if (!textoEntrada || typeof textoEntrada !== 'string') return [];
        var texto = textoEntrada.trim();
        if (!texto) return [];

        // Intento 1: GeoJSON o JSON Array
        if (texto.startsWith('{') || texto.startsWith('[')) {
            try {
                var objetoJson = JSON.parse(texto);
                if (objetoJson.type === 'Polygon' && Array.isArray(objetoJson.coordinates) && objetoJson.coordinates.length > 0) {
                    return objetoJson.coordinates[0].map(function (par) {
                        return [Number(par[1]), Number(par[0])];
                    }).filter(function (coord) { return !isNaN(coord[0]) && !isNaN(coord[1]); });
                }
                if (Array.isArray(objetoJson)) {
                    var listaCoordenadas = [];
                    var arregloPlano = Array.isArray(objetoJson[0]) && Array.isArray(objetoJson[0][0]) ? objetoJson[0] : objetoJson;
                    for (var i = 0; i < arregloPlano.length; i++) {
                        var punto = arregloPlano[i];
                        if (Array.isArray(punto) && punto.length >= 2) {
                            var lat = Number(punto[0]);
                            var lng = Number(punto[1]);
                            if (lat < -50 && lng > -40) {
                                var aux = lat;
                                lat = lng;
                                lng = aux;
                            }
                            if (!isNaN(lat) && !isNaN(lng)) {
                                listaCoordenadas.push([lat, lng]);
                            }
                        }
                    }
                    if (listaCoordenadas.length > 0) return listaCoordenadas;
                }
            } catch (e) {
                // Si falla el parseo estricto, continúa con regex
            }
        }

        // Intento 2: Búsqueda de pares numéricos con expresiones regulares
        var patronPares = /\[?\s*(-?\d{1,2}\.?\d*)\s*[,;\s]\s*(-?\d{1,3}\.?\d*)\s*\]?/g;
        var coincidencias = [];
        var match;
        while ((match = patronPares.exec(texto)) !== null) {
            var num1 = parseFloat(match[1]);
            var num2 = parseFloat(match[2]);
            if (!isNaN(num1) && !isNaN(num2)) {
                var latitud = num1;
                var longitud = num2;
                if (latitud < -50 && longitud > -40) {
                    latitud = num2;
                    longitud = num1;
                }
                coincidencias.push([latitud, longitud]);
            }
        }
        if (coincidencias.length >= 3) return coincidencias;

        // Intento 3: Geocodificación por esquinas, intersecciones y barrios
        var puntosCatalogo = [];
        var partes = texto.split(/[,;\n]+/);
        for (var j = 0; j < partes.length; j++) {
            var parteLimpia = partes[j].trim();
            if (!parteLimpia) continue;
            var geo = geocodificarInterseccion(parteLimpia);
            if (geo) {
                puntosCatalogo.push([geo.lat, geo.lng]);
            }
        }
        if (puntosCatalogo.length >= 3) return puntosCatalogo;

        return coincidencias;
    }

    /**
     * Editor interactivo en tiempo real para delimitar y visualizar una zona de cobertura en Leaflet.
     * Permite escribir ubicaciones/coordenadas o hacer clic directamente sobre el mapa.
     */
    function renderizarEditorPoligonoZona(idContenedor, idCampoTexto, idSelectorColor, idContadorVertices, opciones) {
        var contenedor = document.getElementById(idContenedor);
        if (!contenedor) return null;

        var mapa = instanciasMapas[idContenedor];
        if (!mapa) {
            mapa = L.map(idContenedor, {
                center: [-34.9011, -56.1645],
                zoom: 13,
                zoomControl: true
            });
            L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap</a> contributors'
            }).addTo(mapa);
            instanciasMapas[idContenedor] = mapa;
        }

        var capaPoligono = L.layerGroup().addTo(mapa);
        var campoTexto = document.getElementById(idCampoTexto);
        var selectorColor = document.getElementById(idSelectorColor);
        var spanContador = idContadorVertices ? document.getElementById(idContadorVertices) : null;
        var callbackCambio = (opciones && typeof opciones.onVerticesCambiados === 'function') ? opciones.onVerticesCambiados : null;

        function obtenerColorActual() {
            if (selectorColor && selectorColor.value) return selectorColor.value;
            return '#2563eb';
        }

        function sincronizarTextoYRefrescar(vertices) {
            if (campoTexto) {
                var textoGenerado = '[\n  ' + vertices.map(function (v) {
                    return '[' + v[0].toFixed(4) + ', ' + v[1].toFixed(4) + ']';
                }).join(',\n  ') + '\n]';
                campoTexto.value = textoGenerado;
                campoTexto.dispatchEvent(new Event('input'));
            }
            refrescarPoligono();
        }

        function refrescarPoligono() {
            capaPoligono.clearLayers();
            var valorTexto = campoTexto ? campoTexto.value : '';
            var color = obtenerColorActual();
            var vertices = parsearCoordenadasPoligono(valorTexto);

            if (spanContador) { spanContador.style.display = 'none'; }

            var descripcionesVertices = [];
            vertices.forEach(function (punto, indice) {
                var desc = encontrarEsquinaCercana(punto[0], punto[1]);
                descripcionesVertices.push({
                    indice: indice + 1,
                    lat: punto[0],
                    lng: punto[1],
                    descripcion: desc
                });
            });

            if (vertices.length >= 3) {
                var poligono = L.polygon(vertices, {
                    color: color,
                    weight: 3,
                    fillColor: color,
                    fillOpacity: 0.30
                }).addTo(capaPoligono);

                vertices.forEach(function (punto, indice) {
                    var desc = descripcionesVertices[indice].descripcion;
                    L.circleMarker(punto, {
                        radius: 5,
                        color: color,
                        fillColor: '#ffffff',
                        fillOpacity: 1,
                        weight: 2.5
                    }).bindTooltip((indice + 1) + '. ' + desc, { direction: 'top' })
                      .addTo(capaPoligono);
                });

                mapa.fitBounds(poligono.getBounds(), { padding: [30, 30], maxZoom: 16 });
            } else if (vertices.length > 0) {
                vertices.forEach(function (punto, indice) {
                    var desc = descripcionesVertices[indice].descripcion;
                    L.circleMarker(punto, {
                        radius: 6,
                        color: color,
                        fillColor: color,
                        fillOpacity: 0.8,
                        weight: 2
                    }).bindTooltip((indice + 1) + '. ' + desc, { direction: 'top', permanent: true })
                      .addTo(capaPoligono);
                });
                mapa.setView(vertices[0], 14);
            }

            if (callbackCambio) {
                callbackCambio(vertices, descripcionesVertices);
            }
        }

        if (campoTexto) {
            campoTexto.addEventListener('input', refrescarPoligono);
            campoTexto.addEventListener('change', refrescarPoligono);
        }

        if (selectorColor) {
            selectorColor.addEventListener('input', function () {
                refrescarPoligono();
            });
            selectorColor.addEventListener('change', function () {
                refrescarPoligono();
            });
        }

        mapa.on('click', function (evento) {
            var lat = Number(evento.latlng.lat.toFixed(4));
            var lng = Number(evento.latlng.lng.toFixed(4));
            var verticesActuales = parsearCoordenadasPoligono(campoTexto ? campoTexto.value : '');
            verticesActuales.push([lat, lng]);
            sincronizarTextoYRefrescar(verticesActuales);
        });

        setTimeout(function () {
            mapa.invalidateSize();
            refrescarPoligono();
        }, 150);

        return {
            refrescar: refrescarPoligono,
            obtenerVertices: function () {
                return parsearCoordenadasPoligono(campoTexto ? campoTexto.value : '');
            },
            agregarVertice: function (lat, lng) {
                var vertices = parsearCoordenadasPoligono(campoTexto ? campoTexto.value : '');
                vertices.push([Number(lat), Number(lng)]);
                sincronizarTextoYRefrescar(vertices);
            },
            eliminarVerticeEn: function (indice) {
                var vertices = parsearCoordenadasPoligono(campoTexto ? campoTexto.value : '');
                if (indice >= 0 && indice < vertices.length) {
                    vertices.splice(indice, 1);
                    sincronizarTextoYRefrescar(vertices);
                }
            },
            agregarPorDireccion: async function (textoDireccion) {
                var geo = geocodificarInterseccion(textoDireccion);
                if (!geo) {
                    geo = await geocodificarEnLineaNominatim(textoDireccion);
                }
                if (!geo) return null;
                var vertices = parsearCoordenadasPoligono(campoTexto ? campoTexto.value : '');
                vertices.push([geo.lat, geo.lng]);
                sincronizarTextoYRefrescar(vertices);
                return geo;
            },
            eliminarUltimoVertice: function () {
                var vertices = parsearCoordenadasPoligono(campoTexto ? campoTexto.value : '');
                if (vertices.length > 0) {
                    vertices.pop();
                    sincronizarTextoYRefrescar(vertices);
                }
            },
            limpiarVertices: function () {
                sincronizarTextoYRefrescar([]);
            },
            cambiarColor: function (nuevoColorHex) {
                if (selectorColor) {
                    selectorColor.value = nuevoColorHex;
                    selectorColor.dispatchEvent(new Event('input'));
                }
                refrescarPoligono();
            }
        };
    }

    /**
     * Renderiza en Leaflet la totalidad de las zonas de cobertura configuradas para el operador.
     */
    function renderizarMapaVisualizadorZonas(idContenedor, listaZonas) {
        var contenedor = document.getElementById(idContenedor);
        if (!contenedor) return;

        var mapa = instanciasMapas[idContenedor];
        if (mapa) {
            mapa.remove();
            delete instanciasMapas[idContenedor];
        }

        mapa = L.map(idContenedor, {
            center: [-34.8836, -56.1645],
            zoom: 12,
            zoomControl: true
        });
        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap</a> contributors'
        }).addTo(mapa);
        instanciasMapas[idContenedor] = mapa;

        var limitesTotales = [];

        if (Array.isArray(listaZonas) && listaZonas.length > 0) {
            listaZonas.forEach(function (zona) {
                var color = zona.colorHex || '#2563eb';
                var vertices = parsearCoordenadasPoligono(zona.poligonoGeografico || '');
                if (vertices.length >= 3) {
                    var poligono = L.polygon(vertices, {
                        color: color,
                        weight: 2,
                        fillColor: color,
                        fillOpacity: zona.activa ? 0.30 : 0.10,
                        dashArray: zona.activa ? null : '4, 4'
                    }).addTo(mapa);

                    var totalFranjas = (zona.franjasHorarias ? zona.franjasHorarias.length : (zona.totalFranjas || 0));
                    var contenidoPopup = '<div class="um-popup">' +
                        '<div class="um-popup-titulo" style="color: ' + color + ';">' + escaparHtml(zona.nombre) + '</div>' +
                        '<div class="um-popup-sub">Estado: <strong>' + (zona.activa ? 'Activa' : 'Inactiva') + '</strong></div>' +
                        '<div class="um-popup-meta">' +
                        '<div>Franjas horarias: <strong>' + totalFranjas + '</strong></div>' +
                        '</div>' +
                        '</div>';

                    poligono.bindPopup(contenidoPopup);
                    limitesTotales = limitesTotales.concat(vertices);
                }
            });
        }

        if (limitesTotales.length > 0) {
            mapa.fitBounds(limitesTotales, { padding: [30, 30], maxZoom: 15 });
        } else {
            mapa.setView([-34.9011, -56.1645], 13);
        }

        setTimeout(function () {
            mapa.invalidateSize();
        }, 150);
    }

    window.MapasUltimaMilla = {
        renderizarMapaSeguimientoEnvio: renderizarMapaSeguimientoEnvio,
        renderizarMapaTableroFlota: renderizarMapaTableroFlota,
        enfocarVehiculoEnTablero: enfocarVehiculoEnTablero,
        ajustarVistaGeneralMapa: ajustarVistaGeneralMapa,
        renderizarMapaDetalleRuta: renderizarMapaDetalleRuta,
        enfocarParadaEnRuta: enfocarParadaEnRuta,
        parsearCoordenadasPoligono: parsearCoordenadasPoligono,
        geocodificarInterseccion: geocodificarInterseccion,
        encontrarEsquinaCercana: encontrarEsquinaCercana,
        catalogoIntersecciones: CATALOGO_INTERSECCIONES,
        renderizarEditorPoligonoZona: renderizarEditorPoligonoZona,
        renderizarMapaVisualizadorZonas: renderizarMapaVisualizadorZonas
    };
})(window);
