/**
 * ========================================================
 * BEAUTYSHOP 3D DESIGN SYSTEM - ADMIN JS
 * 3D tiết chế cho Dashboard, Thẻ thống kê & Tương tác giao diện
 * ========================================================
 */

(function (window, document) {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        initAdminDashboard3D();
        initAdminModal3D();
    });

    /**
     * 1. Hiệu ứng 3D tiết chế cho thẻ thống kê (Tilt rất nhẹ: max 3 độ)
     */
    function initAdminDashboard3D() {
        if (!window.BeautyShop3D) return;

        // Chỉ kích hoạt tilt nhẹ cho thẻ thống kê, tuyệt đối không áp dụng lên bảng dữ liệu
        window.BeautyShop3D.initTilt(".stat-card", {
            maxTilt: 3,
            scale: 1.015,
            glare: true,
            maxGlare: 0.2,
            speed: 250
        });
    }

    /**
     * 2. Tương tác Modal nổi 3D
     */
    function initAdminModal3D() {
        const modals = document.querySelectorAll(".modal");
        modals.forEach(function (modal) {
            modal.addEventListener("show.bs.modal", function () {
                const dialog = this.querySelector(".modal-dialog");
                if (dialog) {
                    dialog.style.transform = "scale(0.92) translateZ(0)";
                    setTimeout(function () {
                        dialog.style.transform = "scale(1) translateZ(30px)";
                    }, 20);
                }
            });
        });
    }
})(window, document);
