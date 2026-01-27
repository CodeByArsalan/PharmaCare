/**
 * Main
 */

'use strict';

let menu,
    animate;

document.addEventListener('DOMContentLoaded', function () {
    // class for ios specific styles
    if (navigator.userAgent.match(/iPhone|iPad|iPod/i)) {
        document.body.classList.add('ios');
    }

    (function () {
        // Initialize menu
        //-----------------

        let layoutMenuEls = document.querySelectorAll('#layout-menu');
        layoutMenuEls.forEach(function (element) {
            menu = new Menu(element, {
                orientation: 'vertical',
                closeChildren: false
            });
            // Change parameter to true if you want scroll animation
            window.Helpers.scrollToActive((animate = false));
            window.Helpers.mainMenu = menu;
        });

        // Initialize menu togglers and bind click on each
        let menuToggler = document.querySelectorAll('.layout-menu-toggle');
        menuToggler.forEach(item => {
            item.addEventListener('click', event => {
                event.preventDefault();
                window.Helpers.toggleCollapsed();
            });
        });

        // Display menu toggle (layout-menu-toggle) on hover with delay
        let delay = function (elem, callback) {
            let timeout = null;
            elem.onmouseenter = function () {
                if (!Helpers.isSmallScreen()) {
                    timeout = setTimeout(callback, 300);
                } else {
                    timeout = setTimeout(callback, 0);
                }
            };

            elem.onmouseleave = function () {
                // guard for existence
                const toggleEl = document.querySelector('.layout-menu-toggle');
                if (toggleEl) {
                    toggleEl.classList.remove('d-block');
                }
                clearTimeout(timeout);
            };
        };

        const layoutMenuEl = document.getElementById('layout-menu');
        if (layoutMenuEl) {
            delay(layoutMenuEl, function () {
                // not for small screen
                if (!Helpers.isSmallScreen()) {
                    const toggleEl = document.querySelector('.layout-menu-toggle');
                    if (toggleEl) {
                        toggleEl.classList.add('d-block');
                    }
                }
            });
        }

        // Display in main menu when menu scrolls
        let menuInnerContainer = document.getElementsByClassName('menu-inner');
        let menuInnerShadow = document.getElementsByClassName('menu-inner-shadow')[0];
        if (menuInnerContainer.length > 0 && menuInnerShadow) {
            // guard if ps__thumb-y is not present
            menuInnerContainer[0].addEventListener('ps-scroll-y', function () {
                const thumb = this.querySelector('.ps__thumb-y');
                if (thumb && thumb.offsetTop) {
                    menuInnerShadow.style.display = 'block';
                } else {
                    menuInnerShadow.style.display = 'none';
                }
            });
        }

        // Init helpers & misc
        // --------------------
        // (rest of your init code unchanged)

        // Init BS Tooltip
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });

        // Accordion active class
        const accordionActiveFunction = function (e) {
            if (e.type == 'show.bs.collapse' || e.type == 'show.bs.collapse') {
                const closest = e.target.closest('.accordion-item');
                if (closest) closest.classList.add('active');
            } else {
                const closest = e.target.closest('.accordion-item');
                if (closest) closest.classList.remove('active');
            }
        };

        const accordionTriggerList = [].slice.call(document.querySelectorAll('.accordion'));
        accordionTriggerList.map(function (accordionTriggerEl) {
            accordionTriggerEl.addEventListener('show.bs.collapse', accordionActiveFunction);
            accordionTriggerEl.addEventListener('hide.bs.collapse', accordionActiveFunction);
        });

        // Auto update layout based on screen size
        window.Helpers.setAutoUpdate(true);

        // Toggle Password Visibility
        if (window.Helpers && typeof window.Helpers.initPasswordToggle === 'function') {
            window.Helpers.initPasswordToggle();
        }

        // Speech To Text
        if (window.Helpers && typeof window.Helpers.initSpeechToText === 'function') {
            window.Helpers.initSpeechToText();
        }

        // Manage menu expanded/collapsed with templateCustomizer & local storage
        //------------------------------------------------------------------

        if (window.Helpers && !window.Helpers.isSmallScreen()) {
            // Auto update menu collapsed/expanded based on the themeConfig
            window.Helpers.setCollapsed(true, false);
        }

    })(); // end iife
});

// Utils
function isMacOS() {
  return /Mac|iPod|iPhone|iPad/.test(navigator.userAgent);
}
