/**
 * ========================================================
 * INTRO "GIỌT SERUM RƠI" - BEAUTYSHOP
 * Animation tinh chất hổ phách rơi, tạo sóng & mở màn trang chủ
 * ========================================================
 */

(function () {
    "use strict";

    // HẰNG SỐ CẤU HÌNH THỜI GIAN & TỌA ĐỘ
    const INTRO_CONFIG = {
        WATER_X_PCT: 50,      // 50% chiều ngang màn hình
        WATER_Y_PCT: 60,      // 60% chiều dọc màn hình
        DROP_START: 300,      // 0.3s: Bắt đầu rơi
        DROP_HIT: 1400,       // 1.4s: Chạm mặt nước & lan sóng
        BRAND_FADE: 1700,     // 1.7s: Tên thương hiệu hiện nhẹ
        MASK_START: 2200,     // 2.2s: Bắt đầu mở rộng vòng tròn tan overlay
        MASK_END: 4800,       // 4.8s: Tan hết overlay
        TOTAL_DURATION: 4900, // 4.9s: Kết thúc hoàn toàn
        STORAGE_KEY: "beautyshop_serum_intro_seen"
    };

    let isFinished = false;
    let animFrameId = null;
    let maskTimer = null;
    let endTimer = null;

    function initSerumIntro() {
        const overlay = document.getElementById("serum-intro-overlay");
        const skipBtn = document.getElementById("intro-skip-btn");
        const homeContent = document.getElementById("home-main-content");

        if (!overlay) return;

        // 1. KIỂM TRA PREFERS-REDUCED-MOTION
        const prefersReduced = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        if (prefersReduced) {
            cleanup(overlay, skipBtn, homeContent);
            return;
        }

        // 2. KIỂM TRA SESSIONSTORAGE (CHỈ CHẠY 1 LẦN MỖI PHIÊN)
        try {
            if (sessionStorage.getItem(INTRO_CONFIG.STORAGE_KEY) === "true") {
                cleanup(overlay, skipBtn, homeContent);
                return;
            }
        } catch (err) {
            console.warn("SessionStorage access restricted:", err);
        }

        // 3. KHÓA CUỘN TRANG & CHỐNG TAB VÀO NỘI DUNG PHÍA SAU
        document.documentElement.style.overflow = "hidden";
        document.body.style.overflow = "hidden";

        if (homeContent) {
            homeContent.setAttribute("inert", "");
            homeContent.setAttribute("aria-hidden", "true");
            homeContent.classList.add("intro-zoom-start");
        }

        // 4. BẮT SỰ KIỆN NÚT "BỎ QUA"
        if (skipBtn) {
            skipBtn.addEventListener("click", function (e) {
                e.preventDefault();
                finishIntro(overlay, skipBtn, homeContent);
            });
        }

        // Phím ESC cũng cho phép bỏ qua nhanh
        window.addEventListener("keydown", function onEsc(e) {
            if (e.key === "Escape") {
                window.removeEventListener("keydown", onEsc);
                finishIntro(overlay, skipBtn, homeContent);
            }
        });

        // 5. KÍCH HOẠT HIỆU ỨNG TAN OVERLAY BẰNG MASK-IMAGE (2.2s - 4.8s)
        maskTimer = setTimeout(function () {
            if (isFinished) return;

            // Bắt đầu thu phóng trang chủ từ 105% về 100%
            if (homeContent) {
                homeContent.classList.add("intro-zoom-settle");
            }

            startRadialMaskTransition(overlay, function () {
                finishIntro(overlay, skipBtn, homeContent);
            });
        }, INTRO_CONFIG.MASK_START);

        // Fallback tự động kết thúc nếu mask transition bị delay
        endTimer = setTimeout(function () {
            finishIntro(overlay, skipBtn, homeContent);
        }, INTRO_CONFIG.TOTAL_DURATION + 200);
    }

    /**
     * Mở rộng vòng tròn viền mềm làm tan overlay từ đúng điểm chạm nước (50%, 60%)
     */
    function startRadialMaskTransition(overlay, onComplete) {
        const maskDuration = INTRO_CONFIG.MASK_END - INTRO_CONFIG.MASK_START; // ~2600ms
        let startTime = null;

        const w = window.innerWidth;
        const h = window.innerHeight;
        const originX = w * (INTRO_CONFIG.WATER_X_PCT / 100);
        const originY = h * (INTRO_CONFIG.WATER_Y_PCT / 100);

        // Tính khoảng cách lớn nhất từ điểm chạm đến 4 góc màn hình
        const maxDist = Math.hypot(
            Math.max(originX, w - originX),
            Math.max(originY, h - originY)
        );
        const targetRadius = maxDist * 1.25;

        // Kiểm tra hỗ trợ mask-image
        const supportsMask = CSS && CSS.supports && (
            CSS.supports("mask-image", "radial-gradient(circle, black, transparent)") ||
            CSS.supports("-webkit-mask-image", "radial-gradient(circle, black, transparent)")
        );

        function step(timestamp) {
            if (isFinished) return;
            if (!startTime) startTime = timestamp;

            const elapsed = timestamp - startTime;
            const progress = Math.min(1, elapsed / maskDuration);

            // Easing: ease-in-out mượt mà
            const ease = progress < 0.5
                ? 2 * progress * progress
                : 1 - Math.pow(-2 * progress + 2, 2) / 2;

            if (supportsMask) {
                const currentRadius = ease * targetRadius;
                const softEdge = Math.max(50, currentRadius * 0.22);
                const rInner = Math.max(0, currentRadius - softEdge);
                const rOuter = currentRadius + softEdge;

                // Vùng trong tâm là transparent (để lộ trang chủ), vùng ngoài là black (giữ overlay)
                const maskGradient = `radial-gradient(circle at ${INTRO_CONFIG.WATER_X_PCT}% ${INTRO_CONFIG.WATER_Y_PCT}%, transparent 0%, transparent ${rInner.toFixed(1)}px, rgba(0,0,0,0.4) ${currentRadius.toFixed(1)}px, black ${rOuter.toFixed(1)}px, black 100%)`;

                overlay.style.webkitMaskImage = maskGradient;
                overlay.style.maskImage = maskGradient;
            } else {
                // Fallback nếu trình duyệt không hỗ trợ mask
                overlay.style.opacity = String(1 - ease);
            }

            if (progress < 1) {
                animFrameId = requestAnimationFrame(step);
            } else {
                if (typeof onComplete === "function") onComplete();
            }
        }

        animFrameId = requestAnimationFrame(step);
    }

    /**
     * Dọn dẹp, lưu trạng thái phiên và gỡ bỏ overlay khỏi DOM
     */
    function finishIntro(overlay, skipBtn, homeContent) {
        if (isFinished) return;
        isFinished = true;

        if (animFrameId) {
            cancelAnimationFrame(animFrameId);
            animFrameId = null;
        }
        if (maskTimer) clearTimeout(maskTimer);
        if (endTimer) clearTimeout(endTimer);

        // Lưu vào sessionStorage
        try {
            sessionStorage.setItem(INTRO_CONFIG.STORAGE_KEY, "true");
        } catch (err) {
            console.warn("Could not save to sessionStorage:", err);
        }

        cleanup(overlay, skipBtn, homeContent);
    }

    function cleanup(overlay, skipBtn, homeContent) {
        // Mở lại cuộn trang
        document.documentElement.style.overflow = "";
        document.body.style.overflow = "";

        // Gỡ bỏ inert và aria-hidden
        if (homeContent) {
            homeContent.removeAttribute("inert");
            homeContent.removeAttribute("aria-hidden");
            homeContent.classList.remove("intro-zoom-start");
            homeContent.classList.add("intro-zoom-settle");
            homeContent.style.transform = "";
        }

        // Xóa overlay và nút bỏ qua khỏi DOM
        if (overlay && overlay.parentNode) {
            overlay.parentNode.removeChild(overlay);
        }
        if (skipBtn && skipBtn.parentNode) {
            skipBtn.parentNode.removeChild(skipBtn);
        }
    }

    // Chạy khi DOM đã sẵn sàng
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initSerumIntro);
    } else {
        initSerumIntro();
    }
})();
