/**
 * 强制表格按内容宽度撑开，超出容器时由 .table-wrapper 的 overflow-x:auto 提供滚动
 */
(function () {
    function fixTableScroll() {
        var wrappers = document.querySelectorAll('.table-wrapper');
        for (var i = 0; i < wrappers.length; i++) {
            var wrapper = wrappers[i];
            var table = wrapper.querySelector('table');
            if (!table) continue;

            // 先把所有 CSS 约束去掉，让浏览器算出内容的真实宽度
            table.style.width = 'auto';
            table.style.maxWidth = 'none';
            table.style.minWidth = '0';
            table.style.display = 'inline-table';
            table.style.tableLayout = 'auto';
            table.style.whiteSpace = 'nowrap';

            // 设置所有单元格不换行
            var cells = table.querySelectorAll('th, td');
            for (var j = 0; j < cells.length; j++) {
                cells[j].style.whiteSpace = 'nowrap';
            }

            // 等浏览器完成布局后，拿 scrollWidth 就是内容真实宽度
            // 如果超出容器，就写死 table 的最小宽度
            setTimeout((function (tbl, wrp) {
                return function () {
                    var contentWidth = tbl.scrollWidth;
                    if (contentWidth > 0) {
                        tbl.style.minWidth = contentWidth + 'px';
                        tbl.style.display = 'table';
                    }
                    // 兜底：如果内容宽度不大，至少撑满容器
                    if (tbl.scrollWidth <= wrp.clientWidth) {
                        tbl.style.minWidth = '100%';
                    }
                };
            })(table, wrapper), 100);
        }
    }

    // DOM 就绪后执行
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', fixTableScroll);
    } else {
        fixTableScroll();
    }
})();
