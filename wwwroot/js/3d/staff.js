/**
 * ========================================================
 * BEAUTYSHOP 3D DESIGN SYSTEM - STAFF JS
 * Tối giản, tập trung vào hiệu năng & phản hồi vật lý khi thao tác
 * ========================================================
 */

(function (window, document) {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        initStaffActionsTactile();
    });

    /**
     * Phản hồi xúc giác 3D khi nhân viên bấm các nút tác vụ (Xác nhận đơn, Cập nhật kho)
     */
    function initStaffActionsTactile() {
        const actionButtons = document.querySelectorAll(".btn-ap, .btn-as, .btn-ae, .btn-ad, .btn-aw");
        actionButtons.forEach(function (btn) {
            btn.addEventListener("mousedown", function () {
                this.style.transform = "translateY(2px)";
            });
            btn.addEventListener("mouseup", function () {
                this.style.transform = "";
            });
            btn.addEventListener("mouseleave", function () {
                this.style.transform = "";
            });
        });
    }
})(window, document);
