/**
 * PharmaCare DataTables Global Initialization
 * Auto-initializes all tables with 'dataTable' class that aren't already initialized
 */

$(document).ready(function () {
    // Default DataTables configuration for all tables
    var defaultConfig = {
        pageLength: 15,
        lengthMenu: [[10, 15, 25, 50, 100, -1], [10, 15, 25, 50, 100, "All"]],
        order: [], // Preserve original order by default
        language: {
            search: "Search:",
            lengthMenu: "Show _MENU_ entries",
            info: "Showing _START_ to _END_ of _TOTAL_ entries",
            infoEmpty: "No entries available",
            infoFiltered: "(filtered from _MAX_ total entries)",
            emptyTable: "No data available in table",
            paginate: {
                first: "First",
                last: "Last",
                next: "Next",
                previous: "Previous"
            }
        },
        dom: '<"row"<"col-sm-12 col-md-6"l><"col-sm-12 col-md-6"f>>' +
            '<"row"<"col-sm-12"tr>>' +
            '<"row"<"col-sm-12 col-md-5"i><"col-sm-12 col-md-7"p>>',
        responsive: false,
        autoWidth: false,
        stateSave: false
    };

    // Find all tables with 'dataTable' class that aren't already initialized
    // Use setTimeout to allow page-specific initialization scripts to run first
    setTimeout(function () {
        $('table.dataTable:not(.dataTable-initialized)').each(function () {
            var $table = $(this);

            // Skip if already a DataTables instance
            if ($.fn.DataTable.isDataTable(this)) {
                return;
            }

            // Get table-specific configuration from data attributes
            var tableConfig = $.extend({}, defaultConfig);

            // Check for custom page length
            if ($table.data('page-length')) {
                tableConfig.pageLength = parseInt($table.data('page-length'));
            }

            // Check for custom ordering
            if ($table.data('order')) {
                tableConfig.order = $table.data('order');
            }

            // Check if searching should be disabled
            if ($table.data('searching') === false) {
                tableConfig.searching = false;
            }

            // Check if paging should be disabled
            if ($table.data('paging') === false) {
                tableConfig.paging = false;
            }

            // Check if info should be hidden
            if ($table.data('info') === false) {
                tableConfig.info = false;
            }

            // Initialize DataTable
            try {
                $table.DataTable(tableConfig);
                $table.addClass('dataTable-initialized');
            } catch (error) {
                console.warn('DataTable initialization failed for table:', this, error);
            }
        });

        // Also initialize tables with 'init-datatable' class (explicit opt-in)
        $('table.init-datatable:not(.dataTable-initialized)').each(function () {
            var $table = $(this);

            if ($.fn.DataTable.isDataTable(this)) {
                return;
            }

            try {
                $table.DataTable(defaultConfig);
                $table.addClass('dataTable-initialized');
            } catch (error) {
                console.warn('DataTable initialization failed for table:', this, error);
            }
        });
    }, 100); // Delay 100ms to allow page-specific scripts to initialize first
});

// Helper function to refresh a DataTable after DOM changes
window.refreshDataTable = function (tableSelector) {
    var $table = $(tableSelector);
    if ($.fn.DataTable.isDataTable($table)) {
        $table.DataTable().draw();
    }
};

// Helper function to reinitialize a DataTable after content change
window.reinitDataTable = function (tableSelector, config) {
    var $table = $(tableSelector);

    // Destroy existing instance if present
    if ($.fn.DataTable.isDataTable($table)) {
        $table.DataTable().destroy();
        $table.removeClass('dataTable-initialized');
    }

    // Reinitialize with optional custom config
    var tableConfig = $.extend({}, {
        pageLength: 15,
        lengthMenu: [[10, 15, 25, 50, 100, -1], [10, 15, 25, 50, 100, "All"]],
        order: [],
        responsive: false,
        autoWidth: false
    }, config || {});

    $table.DataTable(tableConfig);
    $table.addClass('dataTable-initialized');
};
