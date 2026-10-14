// ==========================================================================
// Control Universal de Botones de Ayuda Contextual (?) y Popovers
// ==========================================================================
document.addEventListener('DOMContentLoaded', function () {
    var popoverElemento = document.createElement('div');
    popoverElemento.className = 'ayuda-popover';
    popoverElemento.setAttribute('role', 'tooltip');
    document.body.appendChild(popoverElemento);

    var botonActivo = null;

    function cerrarPopover() {
        popoverElemento.classList.remove('visible');
        if (botonActivo) {
            botonActivo.classList.remove('activo');
            botonActivo = null;
        }
    }

    function mostrarPopover(boton) {
        var texto = boton.getAttribute('data-ayuda') || boton.getAttribute('title');
        if (!texto) return;

        if (botonActivo === boton) {
            cerrarPopover();
            return;
        }

        if (botonActivo) {
            botonActivo.classList.remove('activo');
        }

        botonActivo = boton;
        boton.classList.add('activo');

        popoverElemento.textContent = texto;
        popoverElemento.classList.add('visible');

        var rect = boton.getBoundingClientRect();
        var popoverWidth = popoverElemento.offsetWidth;
        var popoverHeight = popoverElemento.offsetHeight;

        // Posicionamiento inteligente: arriba del botón por defecto, o abajo si no hay espacio
        var top = rect.top - popoverHeight - 8;
        var left = rect.left + (rect.width / 2) - (popoverWidth / 2);

        if (top < 10) {
            top = rect.bottom + 8;
        }

        if (left < 10) {
            left = 10;
        } else if (left + popoverWidth > window.innerWidth - 10) {
            left = window.innerWidth - popoverWidth - 10;
        }

        popoverElemento.style.top = top + 'px';
        popoverElemento.style.left = left + 'px';
    }

    // Delegación de eventos para botones de ayuda
    document.addEventListener('click', function (e) {
        var boton = e.target.closest('.btn-ayuda');
        if (boton) {
            e.preventDefault();
            e.stopPropagation();
            mostrarPopover(boton);
        } else if (!e.target.closest('.ayuda-popover')) {
            cerrarPopover();
        }
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            cerrarPopover();
        }
    });

    window.addEventListener('resize', cerrarPopover);
    window.addEventListener('scroll', cerrarPopover, true);
});
