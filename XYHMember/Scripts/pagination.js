(function () {
    var PAGE_SIZE = 20;

    window.initTablePagination = function (tableId, options) {
        options = options || {};
        var pageSize = options.pageSize || PAGE_SIZE;
        var summaryRows = options.summaryRows || 0;

        var table = document.getElementById(tableId);
        if (!table) return;

        var tbody = table.querySelector('tbody');
        if (!tbody) return;

        var allRows = Array.from(tbody.querySelectorAll('tr'));
        if (allRows.length === 0) return;

        // 自动识别 .summary-row 汇总行，同时兼容 summaryRows 参数
        var autoSummary = allRows.filter(function (r) { return r.classList.contains('summary-row'); }).length;
        var skipRows = Math.max(summaryRows, autoSummary);
        var dataRows = allRows.slice(0, allRows.length - skipRows);
        var footerRows = allRows.slice(allRows.length - skipRows);
        var container = document.getElementById('paginationContainer');
        if (!container) return;

        if (dataRows.length <= pageSize) {
            container.innerHTML = '<div class="am-cf" style="margin-top:10px;">共 ' + dataRows.length + ' 条记录</div>';
            return;
        }

        var totalPages = Math.ceil(dataRows.length / pageSize);
        var currentPage = 1;

        function showPage(page) {
            currentPage = page;
            dataRows.forEach(function (r) { r.style.display = 'none'; });
            var start = (page - 1) * pageSize;
            var end = Math.min(start + pageSize, dataRows.length);
            for (var i = start; i < end; i++) {
                dataRows[i].style.display = '';
            }
            footerRows.forEach(function (r) { r.style.display = ''; });
            paint();
        }

        function paint() {
            var h = '<div class="am-cf" style="margin-top:10px;">';
            h += '<span style="float:left;line-height:36px;">共 ' + dataRows.length + ' 条记录，第 ' + currentPage + '/' + totalPages + ' 页</span>';
            h += '<ul class="am-pagination am-pagination-right" style="margin:0;">';

            if (currentPage > 1) {
                h += '<li><a href="javascript:void(0)" data-p="' + (currentPage - 1) + '">«</a></li>';
            } else {
                h += '<li class="am-disabled"><a>«</a></li>';
            }

            var sp = Math.max(1, currentPage - 2);
            var ep = Math.min(totalPages, currentPage + 2);
            if (sp > 1) {
                h += '<li><a href="javascript:void(0)" data-p="1">1</a></li>';
                if (sp > 2) h += '<li><span>...</span></li>';
            }
            for (var p = sp; p <= ep; p++) {
                h += '<li' + (p === currentPage ? ' class="am-active"' : '') + '><a href="javascript:void(0)"' + (p !== currentPage ? ' data-p="' + p + '"' : '') + '>' + p + '</a></li>';
            }
            if (ep < totalPages) {
                if (ep < totalPages - 1) h += '<li><span>...</span></li>';
                h += '<li><a href="javascript:void(0)" data-p="' + totalPages + '">' + totalPages + '</a></li>';
            }

            if (currentPage < totalPages) {
                h += '<li><a href="javascript:void(0)" data-p="' + (currentPage + 1) + '">»</a></li>';
            } else {
                h += '<li class="am-disabled"><a>»</a></li>';
            }
            h += '</ul></div>';
            container.innerHTML = h;

            container.querySelectorAll('[data-p]').forEach(function (a) {
                a.addEventListener('click', function () { showPage(parseInt(this.getAttribute('data-p'))); });
            });
        }

        showPage(1);
    };
})();
