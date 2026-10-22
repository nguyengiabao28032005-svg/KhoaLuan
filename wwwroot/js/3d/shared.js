/**
 * ========================================================
 * BEAUTYSHOP 3D DESIGN SYSTEM - SHARED JS ENGINE
 * Xử lý tương tác Tilt 3D theo con trỏ chuột, Parallax & A11y
 * ========================================================
 */

(function (window, document) {
    "use strict";

    const prefersReducedMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const isTouchDevice = "ontouchstart" in window || navigator.maxTouchPoints > 0;

    const BeautyShop3D = {
        prefersReducedMotion: prefersReducedMotion,
        isTouch: isTouchDevice,

        /**
         * Khởi tạo hiệu ứng Tilt 3D cho danh sách phần tử
         * @param {string|NodeList} targetSelector
         * @param {Object} options
         */
        initTilt: function (targetSelector, options) {
            if (prefersReducedMotion || isTouchDevice) return;

            const opts = Object.assign({
                maxTilt: 8,         // Góc nghiêng tối đa (độ)
                perspective: 1000,  // Phối cảnh (px)
                scale: 1.025,       // Phóng to nhẹ khi hover
                speed: 300,         // Thời gian chuyển động (ms)
                glare: true,        // Bật vệt sáng chạy theo chuột
                maxGlare: 0.45      // Độ sáng vệt phản quang
            }, options);

            const elements = typeof targetSelector === "string" 
                ? document.querySelectorAll(targetSelector) 
                : targetSelector;

            if (!elements || elements.length === 0) return;

            elements.forEach(function (el) {
                if (el.dataset.tiltInitialized) return;
                el.dataset.tiltInitialized = "true";

                // Thêm lớp glare nếu được bật
                let glareEl = null;
                if (opts.glare) {
                    glareEl = el.querySelector(".glare-layer");
                    if (!glareEl) {
                        glareEl = document.createElement("div");
                        glareEl.className = "glare-layer";
                        el.appendChild(glareEl);
                    }
                }

                let animFrame = null;
                let mouseX = 0;
                let mouseY = 0;
                let isHovered = false;

                function updateTransform() {
                    if (!isHovered) return;

                    const rect = el.getBoundingClientRect();
                    const width = rect.width;
                    const height = rect.height;

                    if (width === 0 || height === 0) return;

                    // Tính tọa độ chuột tương đối (-0.5 đến 0.5)
                    const xNorm = (mouseX - rect.left) / width - 0.5;
                    const yNorm = (mouseY - rect.top) / height - 0.5;

                    // Góc nghiêng (rotateX ngược chiều Y chuột, rotateY cùng chiều X chuột)
                    const rotateX = -(yNorm * opts.maxTilt * 2).toFixed(2);
                    const rotateY = (xNorm * opts.maxTilt * 2).toFixed(2);

                    el.style.transform = `perspective(${opts.perspective}px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) scale3d(${opts.scale}, ${opts.scale}, ${opts.scale})`;

                    if (glareEl) {
                        const glareX = ((xNorm + 0.5) * 100).toFixed(1);
                        const glareY = ((yNorm + 0.5) * 100).toFixed(1);
                        glareEl.style.background = `radial-gradient(circle at ${glareX}% ${glareY}%, rgba(255,255,255,${opts.maxGlare}) 0%, rgba(255,255,255,0) 75%)`;
                        glareEl.style.opacity = "1";
                    }

                    animFrame = null;
                }

                el.addEventListener("mouseenter", function () {
                    isHovered = true;
                    el.style.transition = `transform ${opts.speed * 0.4}ms ease-out`;
                });

                el.addEventListener("mousemove", function (e) {
                    mouseX = e.clientX;
                    mouseY = e.clientY;
                    if (!animFrame) {
                        animFrame = requestAnimationFrame(updateTransform);
                    }
                });

                el.addEventListener("mouseleave", function () {
                    isHovered = false;
                    if (animFrame) {
                        cancelAnimationFrame(animFrame);
                        animFrame = null;
                    }
                    el.style.transition = `transform ${opts.speed}ms cubic-bezier(0.16, 1, 0.3, 1)`;
                    el.style.transform = `perspective(${opts.perspective}px) rotateX(0deg) rotateY(0deg) scale3d(1, 1, 1)`;

                    if (glareEl) {
                        glareEl.style.opacity = "0";
                    }
                });

                // Hỗ trợ trợ năng bàn phím (A11y focus)
                el.addEventListener("focus", function () {
                    el.style.transform = `perspective(${opts.perspective}px) translateZ(12px)`;
                });
                el.addEventListener("blur", function () {
                    el.style.transform = `perspective(${opts.perspective}px) translateZ(0)`;
                });
            });
        },

        /**
         * Khởi tạo hiệu ứng Parallax nhiều lớp
         * @param {HTMLElement|string} container
         * @param {string} layerSelector
         */
        initParallax: function (container, layerSelector) {
            if (prefersReducedMotion || isTouchDevice) return;

            const c = typeof container === "string" ? document.querySelector(container) : container;
            if (!c) return;

            const layers = c.querySelectorAll(layerSelector || ".float-layer");
            if (layers.length === 0) return;

            let animId = null;
            let targetX = 0;
            let targetY = 0;

            function updateParallax() {
                layers.forEach(function (layer) {
                    const depth = parseFloat(layer.dataset.depth || "0.1");
                    const moveX = (targetX * depth * 40).toFixed(2);
                    const moveY = (targetY * depth * 40).toFixed(2);
                    const rot = (targetX * depth * 15).toFixed(2);
                    layer.style.transform = `translate3d(${moveX}px, ${moveY}px, 0) rotate(${rot}deg)`;
                });
                animId = null;
            }

            c.addEventListener("mousemove", function (e) {
                const rect = c.getBoundingClientRect();
                targetX = (e.clientX - rect.left) / rect.width - 0.5;
                targetY = (e.clientY - rect.top) / height - 0.5;
                if (!animId) {
                    animId = requestAnimationFrame(updateParallax);
                }
            });

            c.addEventListener("mouseleave", function () {
                if (animId) cancelAnimationFrame(animId);
                layers.forEach(function (layer) {
                    layer.style.transform = "translate3d(0, 0, 0) rotate(0deg)";
                });
            });
        }
    };

    window.BeautyShop3D = BeautyShop3D;

    // Tự động khởi chạy các phần tử có class .tilt
    document.addEventListener("DOMContentLoaded", function () {
        BeautyShop3D.initTilt(".tilt, .card-3d.with-tilt", { maxTilt: 8 });
    });
})(window, document);
