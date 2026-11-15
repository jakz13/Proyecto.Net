/*
 * Código de barras Code 128 (conjunto B) para las etiquetas de bultos del Backoffice.
 * Es el mismo algoritmo y la misma tabla que GeneradorDocumentoEtiquetas (C#): una prueba compara ambas tablas para que no se desfasen.
 * Antes las ventanas dibujaban un patrón inventado que ningún lector podía decodificar.
 */
(function (global) {
    'use strict';

    var PATRONES = [
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
        "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
        "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
        "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
        "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
        "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
        "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
        "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
        "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
        "114131","311141","411131","211412","211214","211232","2331112"
    ];
    var INICIO_B = 104, PARADA = 106, ZONA_SILENCIO = 10;

    function escapar(texto) {
        return String(texto).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    // Devuelve [{x, ancho}] en módulos, con la zona de silencio de 10 módulos al inicio.
    function barras(codigo) {
        var texto = (codigo || '').trim().toUpperCase() || 'TRK-0000';
        var simbolos = [INICIO_B];
        for (var i = 0; i < texto.length; i++) {
            var c = texto.charCodeAt(i);
            simbolos.push(c >= 32 && c <= 126 ? c - 32 : '-'.charCodeAt(0) - 32);
        }
        var suma = simbolos[0];
        for (var j = 1; j < simbolos.length; j++) suma += simbolos[j] * j;
        simbolos.push(suma % 103, PARADA);

        var resultado = [], cursor = ZONA_SILENCIO;
        simbolos.forEach(function (s) {
            var patron = PATRONES[s];
            for (var k = 0; k < patron.length; k++) {
                var ancho = patron.charCodeAt(k) - 48;
                if (k % 2 === 0) resultado.push({ x: cursor, ancho: ancho });
                cursor += ancho;
            }
        });
        return { barras: resultado, ancho: cursor + ZONA_SILENCIO };
    }

    // SVG listo para insertar; el viewBox se ajusta al largo del código y el navegador lo escala al ancho del contenedor.
    function svg(codigo, alto) {
        var texto = (codigo || '').trim().toUpperCase() || 'TRK-0000';
        var datos = barras(texto);
        var altoBarra = (alto || 54) - 8;
        var rects = datos.barras.map(function (b) {
            return '<rect x="' + b.x + '" y="4" width="' + b.ancho + '" height="' + altoBarra + '" fill="#0f172a"/>';
        }).join('');
        // Sin alto fijo en píxeles: el SVG se escala con el ancho del contenedor, así cada módulo mide varios píxeles y la cámara del teléfono lo lee desde la pantalla
        return '<svg viewBox="0 0 ' + datos.ancho + ' ' + (alto || 54) + '" width="100%" style="height: auto; max-height: 120px;" role="img" aria-label="Código de barras ' +
            escapar(texto) + '" class="border rounded bg-white p-1" preserveAspectRatio="xMidYMid meet">' + rects + '</svg>';
    }

    global.Code128 = { barras: barras, svg: svg, patrones: PATRONES };
})(window);
