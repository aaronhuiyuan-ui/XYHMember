/*!
 * flatpickr 中文语言包（全局生效）
 * 说明：项目自带的 flatpickr.js 为定制压缩版，仅内置英文语言包，
 *       此文件注册中文语言并设为默认，使所有日期选择器显示中文/数字。
 */
(function () {
    if (!window.flatpickr) {
        return;
    }

    window.flatpickr.l10ns.zh = {
        weekdays: {
            shorthand: ["日", "一", "二", "三", "四", "五", "六"],
            longhand: ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"]
        },
        months: {
            shorthand: ["1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月"],
            longhand: ["一月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月", "十月", "十一月", "十二月"]
        },
        rangeSeparator: " 至 ",
        weekAbbreviation: "周",
        scrollTitle: "滚动切换",
        toggleTitle: "点击切换 12/24 小时时制",
        firstDayOfWeek: 1, // 周一开始
        time_24hr: true     // 24 小时制，避免显示 AM/PM
    };

    window.flatpickr.localize(window.flatpickr.l10ns.zh);
})();
