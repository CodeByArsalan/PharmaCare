(function (window, $) {
    if (!$ || !$.fn || !$.fn.select2) {
        return;
    }

    const SELECTOR = "select:not(.no-select2):not(.select2-manual)";

    function getPlaceholder(el) {
        const explicit = el.getAttribute("data-placeholder");
        if (explicit && explicit.trim().length > 0) {
            return explicit.trim();
        }

        if (el.options && el.options.length > 0) {
            const first = el.options[0];
            if (first && first.value === "" && first.text) {
                return first.text.trim();
            }
        }

        return "Select option";
    }

    function getDropdownParent($el) {
        const $modal = $el.closest(".modal");
        return $modal.length ? $modal : $(document.body);
    }

    function isDataTablesControl(el) {
        if (!el || !el.closest) {
            return false;
        }

        if (el.closest(".dataTables_length, .dt-length")) {
            return true;
        }

        return !!(el.classList.contains("dt-input") && el.name && el.name.endsWith("_length"));
    }

    function canInitialize(el) {
        if (!el || el.tagName !== "SELECT") {
            return false;
        }

        if (el.classList.contains("select2-hidden-accessible")) {
            return false;
        }

        if (el.classList.contains("no-select2") || el.classList.contains("select2-manual")) {
            return false;
        }

        if (isDataTablesControl(el)) {
            return false;
        }

        return true;
    }

    function initOne(el) {
        if (!canInitialize(el)) {
            return;
        }

        const $el = $(el);
        const isMultiple = !!el.multiple;
        const allowClear = !isMultiple && !el.required;

        $el.select2({
            theme: "bootstrap-5",
            width: "100%",
            placeholder: getPlaceholder(el),
            allowClear: allowClear,
            dropdownParent: getDropdownParent($el)
        });
    }

    function initAll(root) {
        const context = root || document;
        $(context).find(SELECTOR).each(function () {
            initOne(this);
        });
    }

    function reinit(el) {
        const $el = $(el);
        if ($el.hasClass("select2-hidden-accessible")) {
            $el.select2("destroy");
        }
        initOne($el[0]);
    }

    $(document).ready(function () {
        initAll(document);
    });

    $(document).on("shown.bs.modal", ".modal", function () {
        initAll(this);
    });

    const observer = new MutationObserver(function (mutations) {
        mutations.forEach(function (mutation) {
            mutation.addedNodes.forEach(function (node) {
                if (!node || node.nodeType !== 1) {
                    return;
                }

                if (node.matches && node.matches("select")) {
                    initOne(node);
                }

                initAll(node);
            });
        });
    });

    observer.observe(document.body, {
        childList: true,
        subtree: true
    });

    window.PharmaCareSelect2 = {
        init: initAll,
        reinit: reinit
    };
})(window, window.jQuery);
