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

    // ==========================================================================
    // Control de Barra Lateral Anclada y Colapsable
    // ==========================================================================
    var botonColapsar = document.getElementById('btnColapsarSidebar');
    if (botonColapsar) {
        // Sincronizar estado inicial guardado en localStorage
        var estaColapsadaGuardada = localStorage.getItem('sidebar_colapsada') === 'true';
        if (estaColapsadaGuardada) {
            document.body.classList.add('sidebar-colapsada');
            document.documentElement.classList.add('sidebar-colapsada');
        }

        botonColapsar.addEventListener('click', function () {
            var colapsar = !document.body.classList.contains('sidebar-colapsada');
            document.body.classList.toggle('sidebar-colapsada', colapsar);
            document.documentElement.classList.toggle('sidebar-colapsada', colapsar);
            localStorage.setItem('sidebar_colapsada', colapsar ? 'true' : 'false');
        });
    }

    // ==========================================================================
    // Control Universal de Cierre de Modales
    // ==========================================================================
    document.addEventListener('click', function (e) {
        var botonCerrar = e.target.closest('[data-bs-dismiss="modal"]');
        if (botonCerrar) {
            var modal = botonCerrar.closest('.modal');
            if (modal) {
                if (window.bootstrap && window.bootstrap.Modal) {
                    var instancia = bootstrap.Modal.getInstance(modal);
                    if (instancia) {
                        instancia.hide();
                        return;
                    }
                }
                modal.classList.remove('show');
                modal.style.display = 'none';
                document.body.classList.remove('modal-open');
                document.querySelectorAll('.modal-backdrop').forEach(function (b) { b.remove(); });
            }
        }
    });
});
