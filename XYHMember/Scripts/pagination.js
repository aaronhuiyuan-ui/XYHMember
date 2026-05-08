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

        // 自动识别 .summary-row 汇总行
        var autoSummary = allRows.filter(function (r) { return r.classList.contains('summary-row'); }).length;
        var skipRows = Math.max(summaryRows, autoSummary);
        var dataRows = allRows.slice(0, allRows.length - skipRows);
        var footerRows = allRows.slice(allRows.length - skipRows);
        var container = document.getElementById('paginationContainer');
        if (!container) return;

        // 数据不足一页时不显示分页
        if (dataRows.length <= pageSize) {
            container.innerHTML = '<div class="am-cf" style="margin-top:10px;color:#888;">共 ' + dataRows.length + ' 条记录</div>';
            return;
        }

        var totalPages = Math.ceil(dataRows.length / pageSize);
        var currentPage = 1;

        function showPage(page) {
            if (page < 1 || page > totalPages) return;
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

        function goToPage() {
            var input = document.getElementById('pageJumpInput');
            if (!input) return;
            var page = parseInt(input.value, 10);
            if (isNaN(page) || page < 1 || page > totalPages) {
                input.value = currentPage;
                return;
            }
            showPage(page);
        }

        function paint() {
            var h = '';

            h += '<div class="am-cf" style="margin-top:10px;line-height:36px;color:#666;">';

            // 记录条数
            h += '共 <b>' + dataRows.length + '</b> 条记录，第 <b>' + currentPage + '</b> / <b>' + totalPages + '</b> 页&nbsp;&nbsp;';

            // 上一页
            if (currentPage > 1) {
                h += '<a href="javascript:void(0)" data-p="' + (currentPage - 1) + '" style="margin:0 2px;">上一页</a>';
            }

            // 页码
            var sp = Math.max(1, currentPage - 2);
            var ep = Math.min(totalPages, currentPage + 2);
            if (sp > 1) {
                h += '<a href="javascript:void(0)" data-p="1" style="margin:0 2px;">1</a>';
                if (sp > 2) h += '<span style="margin:0 2px;">…</span>';
            }
            for (var p = sp; p <= ep; p++) {
                if (p === currentPage) {
                    h += '<span style="margin:0 2px;font-weight:bold;color:#333;">' + p + '</span>';
                } else {
                    h += '<a href="javascript:void(0)" data-p="' + p + '" style="margin:0 2px;">' + p + '</a>';
                }
            }
            if (ep < totalPages) {
                if (ep < totalPages - 1) h += '<span style="margin:0 2px;">…</span>';
                h += '<a href="javascript:void(0)" data-p="' + totalPages + '" style="margin:0 2px;">' + totalPages + '</a>';
            }

            // 下一页
            if (currentPage < totalPages) {
                h += '<a href="javascript:void(0)" data-p="' + (currentPage + 1) + '" style="margin:0 2px;">下一页</a>';
            }

            h += '&nbsp;&nbsp;';

            // 跳转
            h += '跳至 ';
            h += '<input type="number" id="pageJumpInput" value="' + currentPage + '" min="1" max="' + totalPages + '" ';
            h += 'style="width:50px;height:30px;text-align:center;vertical-align:middle;" />';
            h += ' 页 ';
            h += '<button class="am-btn am-btn-default am-btn-sm" id="pageJumpBtn" style="vertical-align:middle;">GO</button>';

            h += '</div>';

            container.innerHTML = h;

            // 绑定页码点击
            container.querySelectorAll('[data-p]').forEach(function (a) {
                a.addEventListener('click', function () {
                    showPage(parseInt(this.getAttribute('data-p')));
                });
            });

            // 绑定跳转按钮
            var jumpBtn = document.getElementById('pageJumpBtn');
            if (jumpBtn) {
                jumpBtn.addEventListener('click', goToPage);
            }
            // 回车跳转
            var jumpInput = document.getElementById('pageJumpInput');
            if (jumpInput) {
                jumpInput.addEventListener('keydown', function (e) {
                    if (e.key === 'Enter') goToPage();
                });
            }
        }

        showPage(1);
    };
})();
