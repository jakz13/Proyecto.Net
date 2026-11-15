/*
 * Ayuda para elegir contraseñas en los formularios del Backoffice: medidor de seguridad, requisitos en vivo, botón para ver lo escrito y bloqueo del
 * envío mientras la contraseña no cumple. Reproduce la regla del servidor (PoliticaDePassword en C#): una prueba compara el largo mínimo y la lista de
 * contraseñas triviales para que no se desfasen. El servidor sigue validando igual; esto solo evita que el panel se cierre con un error al enviar.
 *
 * Uso: <input type="password" data-politica-password> (con data-politica-opcional si el campo puede quedar vacío).
 */
(function (global) {
    'use strict';

    var LONGITUD_MINIMA = 10;
    var TRIVIALES = ['password', 'contrasena', 'contraseña', '1234567890', 'qwertyuiop', 'admin12345', 'letmein123', 'bienvenido1',
        'password123', 'contrasena123', 'administrador'];

    function clases(valor) {
        return {
            minusculas: /\p{Ll}/u.test(valor),
            mayusculas: /\p{Lu}/u.test(valor),
            numeros: /\p{Nd}/u.test(valor),
            simbolos: /[^\p{L}\p{Nd}]/u.test(valor)
        };
    }

    function distintos(valor) {
        return new Set(Array.from(valor)).size;
    }

    // Misma regla y mismos mensajes que PoliticaDePassword.Validar; devuelve null si la contraseña es aceptable.
    function validar(password) {
        if (password === null || password === undefined || String(password).trim() === '') return 'La contraseña no puede ser vacía.';
        var valor = String(password).trim();
        if (valor.length < LONGITUD_MINIMA) return 'La contraseña debe tener al menos ' + LONGITUD_MINIMA + ' caracteres.';
        var c = clases(valor);
        var cantidad = (c.minusculas ? 1 : 0) + (c.mayusculas ? 1 : 0) + (c.numeros ? 1 : 0) + (c.simbolos ? 1 : 0);
        if (cantidad < 3) return 'La contraseña debe combinar al menos tres de estos tipos: minúsculas, mayúsculas, números y símbolos.';
        if (TRIVIALES.indexOf(valor.toLowerCase()) >= 0 || distintos(valor) <= 3) return 'La contraseña es demasiado fácil de adivinar.';
        return null;
    }

    // 0 a 4 para el medidor; solo llega a "Aceptable" (2) o más si cumple la política.
    function puntaje(password) {
        var valor = String(password || '').trim();
        if (valor === '') return 0;
        var c = clases(valor);
        var cantidad = (c.minusculas ? 1 : 0) + (c.mayusculas ? 1 : 0) + (c.numeros ? 1 : 0) + (c.simbolos ? 1 : 0);
        var pts = 0;
        if (valor.length >= 6) pts++;
        if (cantidad >= 3 && valor.length >= LONGITUD_MINIMA) pts++;
        if (valor.length >= 12 && cantidad >= 3) pts++;
        if (valor.length >= 14 && cantidad === 4) pts++;
        if (validar(valor) !== null) pts = Math.min(pts, 1);
        return pts;
    }

    var ETIQUETAS = ['Muy débil', 'Débil', 'Aceptable', 'Fuerte', 'Muy fuerte'];
    var COLORES = ['bg-danger', 'bg-danger', 'bg-warning', 'bg-success', 'bg-success'];

    var ICONO_OJO = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path><circle cx="12" cy="12" r="3"></circle></svg>';
    var ICONO_OJO_TACHADO = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"></path><line x1="1" y1="1" x2="23" y2="23"></line></svg>';

    function requisito(texto) {
        var li = document.createElement('li');
        li.className = 'text-secondary';
        li.dataset.texto = texto;
        li.textContent = '✗ ' + texto;
        return li;
    }

    function marcar(li, cumple) {
        li.className = cumple ? 'text-success' : 'text-secondary';
        li.textContent = (cumple ? '✓ ' : '✗ ') + li.dataset.texto;
    }

    function preparar(input) {
        if (input.dataset.politicaLista === '1') return;
        input.dataset.politicaLista = '1';
        var opcional = input.hasAttribute('data-politica-opcional');
        input.setAttribute('minlength', String(LONGITUD_MINIMA));
        input.setAttribute('autocomplete', 'new-password');

        // Campo + botón del ojito
        var grupo = document.createElement('div');
        grupo.className = 'input-group input-group-sm';
        input.parentNode.insertBefore(grupo, input);
        grupo.appendChild(input);
        var ojo = document.createElement('button');
        ojo.type = 'button';
        ojo.className = 'btn btn-outline-secondary';
        ojo.innerHTML = ICONO_OJO;
        ojo.title = 'Mostrar la contraseña';
        ojo.setAttribute('aria-label', 'Mostrar la contraseña');
        ojo.setAttribute('aria-pressed', 'false');
        grupo.appendChild(ojo);
        ojo.addEventListener('click', function () {
            var visible = input.type === 'password';
            input.type = visible ? 'text' : 'password';
            ojo.innerHTML = visible ? ICONO_OJO_TACHADO : ICONO_OJO;
            ojo.title = visible ? 'Ocultar la contraseña' : 'Mostrar la contraseña';
            ojo.setAttribute('aria-label', ojo.title);
            ojo.setAttribute('aria-pressed', visible ? 'true' : 'false');
        });

        // Medidor, requisitos y mensaje
        var panel = document.createElement('div');
        panel.className = 'mt-1';
        panel.hidden = true;
        var barraFondo = document.createElement('div');
        barraFondo.className = 'progress';
        barraFondo.style.height = '6px';
        var barra = document.createElement('div');
        barra.className = 'progress-bar';
        barra.setAttribute('role', 'progressbar');
        barra.setAttribute('aria-valuemin', '0');
        barra.setAttribute('aria-valuemax', '4');
        barraFondo.appendChild(barra);
        var etiqueta = document.createElement('div');
        etiqueta.className = 'small fw-semibold mt-1';
        var lista = document.createElement('ul');
        lista.className = 'list-unstyled small mb-1 mt-1';
        var rLargo = requisito('Al menos ' + LONGITUD_MINIMA + ' caracteres');
        var rTipos = requisito('Combina 3 de 4 tipos: minúsculas, mayúsculas, números y símbolos');
        var rFacil = requisito('No es fácil de adivinar (nada de "password", "1234567890" ni caracteres repetidos)');
        lista.appendChild(rLargo); lista.appendChild(rTipos); lista.appendChild(rFacil);
        var mensaje = document.createElement('div');
        mensaje.className = 'text-danger small';
        mensaje.setAttribute('aria-live', 'polite');
        panel.appendChild(barraFondo); panel.appendChild(etiqueta); panel.appendChild(lista); panel.appendChild(mensaje);
        grupo.parentNode.insertBefore(panel, grupo.nextSibling);

        var intentoEnvio = false;

        function actualizar() {
            var valor = input.value;
            var vacioOpcional = opcional && valor.trim() === '';
            var motivo = vacioOpcional ? null : validar(valor);

            // Un campo opcional vacío no se mide ni se bloquea
            panel.hidden = vacioOpcional || (valor === '' && !intentoEnvio);

            var recortado = valor.trim();
            var c = clases(recortado);
            var cantidad = (c.minusculas ? 1 : 0) + (c.mayusculas ? 1 : 0) + (c.numeros ? 1 : 0) + (c.simbolos ? 1 : 0);
            marcar(rLargo, recortado.length >= LONGITUD_MINIMA);
            marcar(rTipos, cantidad >= 3);
            marcar(rFacil, recortado !== '' && TRIVIALES.indexOf(recortado.toLowerCase()) < 0 && distintos(recortado) > 3);

            var pts = puntaje(valor);
            barra.className = 'progress-bar ' + COLORES[pts];
            barra.style.width = ((pts + 1) * 20) + '%';
            barra.setAttribute('aria-valuenow', String(pts));
            etiqueta.textContent = 'Seguridad: ' + ETIQUETAS[pts];
            etiqueta.className = 'small fw-semibold mt-1 ' + (motivo === null ? 'text-success' : 'text-secondary');

            input.setCustomValidity(motivo === null ? '' : motivo);
            mensaje.textContent = (valor !== '' || intentoEnvio) && motivo !== null ? motivo : '';
        }

        input.addEventListener('input', actualizar);
        var formulario = input.form;
        if (formulario) {
            formulario.addEventListener('submit', function (e) {
                intentoEnvio = true;
                actualizar();
                if (!input.checkValidity()) {
                    e.preventDefault();
                    e.stopImmediatePropagation();
                    input.focus();
                }
            }, true);
            formulario.addEventListener('reset', function () { setTimeout(function () { intentoEnvio = false; actualizar(); }, 0); });
        }
        actualizar();
    }

    function iniciar() {
        Array.prototype.forEach.call(document.querySelectorAll('input[data-politica-password]'), preparar);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
    else iniciar();

    global.PoliticaPassword = { validar: validar, puntaje: puntaje, longitudMinima: LONGITUD_MINIMA, triviales: TRIVIALES };
})(window);
