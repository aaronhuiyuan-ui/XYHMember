



document.querySelectorAll("[id^='reset-button-']").forEach(function (btn) {
    btn.addEventListener("click", function () {
        var confirmed = window.confirm("确定要重置密码吗？");
        if (!confirmed) return;

        // 从按钮 id "reset-button-<userId>" 中提取 userId
        var userId = btn.id.split("-").pop();
        var backendUrl = "/User/UpdatePass";

        $.ajax({
            type: 'POST',
            url: backendUrl,
            data: { userId: userId },
            success: function (data) {
                if (data.success) {
                    alert("密码重置成功");
                }
            },
            error: function () {
                alert("重置失败，请重试");
            }
        });
    });
});





