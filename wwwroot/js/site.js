// ============================================
// BEAUTYSHOP - Premium JS v2.0
// ============================================

document.addEventListener("DOMContentLoaded", function () {

    // 1. HEADER SHRINK ON SCROLL
    const header = document.querySelector(".main-header");
    const nav = document.querySelector(".main-menu");
    if (header) {
        window.addEventListener("scroll", function () {
            if (window.scrollY > 60) {
                header.classList.add("scrolled");
                if (nav) nav.style.top = "58px";
            } else {
                header.classList.remove("scrolled");
                if (nav) nav.style.top = "70px";
            }
        });
    }

    // 2. SCROLL REVEAL ANIMATION
    const revealEls = document.querySelectorAll(".reveal");
    if (revealEls.length > 0) {
        const observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add("is-visible");
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.12 });
        revealEls.forEach(function (el) { observer.observe(el); });
    }

    // 3. AUTO REVEAL for common sections
    const autoReveal = document.querySelectorAll(
        ".product-item-card, .category-box, .brand-card, .trust-feature-box, .section-title-hasaki"
    );
    autoReveal.forEach(function (el, idx) {
        el.classList.add("reveal");
        el.style.transitionDelay = (idx % 4) * 0.07 + "s";
    });
    // Re-observe after adding class
    if (revealEls.length === 0) {
        const obs = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add("is-visible");
                    obs.unobserve(entry.target);
                }
            });
        }, { threshold: 0.1 });
        autoReveal.forEach(function (el) { obs.observe(el); });
    }

    // 4. FLASH SALE COUNTDOWN TIMER
    const countdownEls = {
        h: document.getElementById("countdown-h"),
        m: document.getElementById("countdown-m"),
        s: document.getElementById("countdown-s")
    };

    if (countdownEls.h && countdownEls.m && countdownEls.s) {
        let hours = parseInt(countdownEls.h.textContent) || 2;
        let minutes = parseInt(countdownEls.m.textContent) || 15;
        let seconds = parseInt(countdownEls.s.textContent) || 45;
        let totalSeconds = hours * 3600 + minutes * 60 + seconds;

        function updateCountdown() {
            if (totalSeconds <= 0) {
                clearInterval(timer);
                return;
            }
            totalSeconds--;
            let h = Math.floor(totalSeconds / 3600);
            let m = Math.floor((totalSeconds % 3600) / 60);
            let s = totalSeconds % 60;
            countdownEls.h.textContent = String(h).padStart(2, "0");
            countdownEls.m.textContent = String(m).padStart(2, "0");
            countdownEls.s.textContent = String(s).padStart(2, "0");
        }

        const timer = setInterval(updateCountdown, 1000);
    }

    // 5. TOAST NOTIFICATION (replaces alert)
    window.showToast = function (message, type) {
        type = type || "success";
        const container = document.getElementById("toast-container") || createToastContainer();

        const toast = document.createElement("div");
        toast.className = "toast align-items-center text-bg-" + type + " border-0 show mb-2";
        toast.setAttribute("role", "alert");
        toast.style.cssText = "animation: fadeInUp 0.3s ease; min-width: 280px;";
        toast.innerHTML =
            '<div class="d-flex">' +
            '<div class="toast-body fw-semibold">' + message + "</div>" +
            '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>' +
            "</div>";

        container.appendChild(toast);

        setTimeout(function () {
            toast.style.opacity = "0";
            toast.style.transform = "translateX(100%)";
            toast.style.transition = "all 0.3s ease";
            setTimeout(function () { toast.remove(); }, 300);
        }, 3500);
    };

    function createToastContainer() {
        const c = document.createElement("div");
        c.id = "toast-container";
        c.className = "toast-container position-fixed bottom-0 end-0 p-3";
        c.style.zIndex = "9999";
        document.body.appendChild(c);
        return c;
    }

    // 6. WISHLIST BUTTON ANIMATION
    document.querySelectorAll(".wishlist-quick-btn, .btn-wishlist").forEach(function (btn) {
        btn.addEventListener("click", function (e) {
            const icon = this.querySelector("i");
            if (icon) {
                icon.classList.remove("bi-heart");
                icon.classList.add("bi-heart-fill");
                this.style.color = "#e53935";
                icon.style.animation = "heartbeat 0.8s ease";
                setTimeout(function () { icon.style.animation = ""; }, 800);
            }
        });
    });

    // 7. SMOOTH NUMBER COUNT (for stat cards)
    function animateCount(el, target) {
        let current = 0;
        const step = target / 50;
        const interval = setInterval(function () {
            current += step;
            if (current >= target) {
                el.textContent = target.toLocaleString("vi-VN");
                clearInterval(interval);
            } else {
                el.textContent = Math.floor(current).toLocaleString("vi-VN");
            }
        }, 30);
    }

    // 8. PRODUCT IMAGE LAZY LOAD EFFECT
    const lazyImages = document.querySelectorAll("img[data-src]");
    if (lazyImages.length > 0) {
        const imgObserver = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    const img = entry.target;
                    img.src = img.dataset.src;
                    img.removeAttribute("data-src");
                    imgObserver.unobserve(img);
                }
            });
        });
        lazyImages.forEach(function (img) { imgObserver.observe(img); });
    }

    // 9. STICKY CART COUNT UPDATE
    function updateCartBadge(count) {
        let badge = document.getElementById("header-cart-badge") || document.querySelector(".cart-badge");
        if (badge) {
            badge.textContent = count;
            badge.style.display = (count > 0 ? "inline-flex" : "none");
            badge.classList.remove("cart-badge-pop");
            void badge.offsetWidth; // Trigger reflow
            badge.classList.add("cart-badge-pop");
        }
    }

    // 10. 3D FLY-TO-CART ANIMATION ENGINE
    window.flyToCart = function (sourceImgEl, targetCartEl, callback) {
        // Support jQuery or DOM element
        if (sourceImgEl && sourceImgEl.jquery) sourceImgEl = sourceImgEl[0];
        if (targetCartEl && targetCartEl.jquery) targetCartEl = targetCartEl[0];

        if (!targetCartEl) {
            targetCartEl = document.getElementById("header-cart") || document.querySelector(".bi-cart3");
        }

        if (!sourceImgEl || !targetCartEl) {
            if (typeof callback === "function") callback();
            return;
        }

        const startRect = sourceImgEl.getBoundingClientRect();
        const targetRect = targetCartEl.getBoundingClientRect();

        // Viewport bounds check
        if (startRect.width === 0 || startRect.height === 0) {
            if (typeof callback === "function") callback();
            return;
        }

        // Viewport overlay container
        let viewport = document.getElementById("fly-to-cart-viewport");
        if (!viewport) {
            viewport = document.createElement("div");
            viewport.id = "fly-to-cart-viewport";
            viewport.className = "fly-to-cart-viewport";
            document.body.appendChild(viewport);
        }

        // Flying card size (proportional, max 160px for elegant look)
        const maxStartDim = 150;
        const aspect = startRect.width / startRect.height;
        let cardW = aspect >= 1 ? maxStartDim : maxStartDim * aspect;
        let cardH = aspect >= 1 ? maxStartDim / aspect : maxStartDim;

        // Start Center
        const p0x = startRect.left + startRect.width / 2;
        const p0y = startRect.top + startRect.height / 2;

        // Target Center
        const p2x = targetRect.left + targetRect.width / 2;
        const p2y = targetRect.top + targetRect.height / 2;

        // Parabolic Bezier Control Point (arc upward towards header)
        const p1x = (p0x + p2x) / 2 - 40;
        const arcHeight = Math.max(140, Math.abs(p0y - p2y) * 0.45);
        const p1y = Math.min(p0y, p2y) - arcHeight;

        // Create 3D card element
        const wrapper = document.createElement("div");
        wrapper.className = "fly-3d-card-wrapper";
        wrapper.style.width = cardW + "px";
        wrapper.style.height = cardH + "px";

        const card = document.createElement("div");
        card.className = "fly-3d-card";

        const imgClone = document.createElement("img");
        imgClone.src = sourceImgEl.src || sourceImgEl.getAttribute("src");
        imgClone.alt = "Flying Product";

        const sheen = document.createElement("div");
        sheen.className = "fly-3d-sheen";

        card.appendChild(imgClone);
        card.appendChild(sheen);
        wrapper.appendChild(card);
        viewport.appendChild(wrapper);

        const duration = 850; // ms
        let startTime = null;
        let lastParticleTime = 0;

        const particleColors = ["#006E4A", "#009665", "#4db88a", "#f39c11", "#ffd200", "#ffffff"];

        function spawnParticle(x, y) {
            const particle = document.createElement("div");
            particle.className = "fly-particle";
            const size = Math.floor(Math.random() * 8) + 6;
            const color = particleColors[Math.floor(Math.random() * particleColors.length)];
            const offsetX = (Math.random() - 0.5) * 24;
            const offsetY = (Math.random() - 0.5) * 24;

            particle.style.width = size + "px";
            particle.style.height = size + "px";
            particle.style.background = color;
            particle.style.boxShadow = "0 0 10px " + color;
            particle.style.left = (x + offsetX) + "px";
            particle.style.top = (y + offsetY) + "px";

            document.body.appendChild(particle);
            setTimeout(function () { particle.remove(); }, 650);
        }

        function animate(timestamp) {
            if (!startTime) startTime = timestamp;
            const elapsed = timestamp - startTime;
            const progress = Math.min(elapsed / duration, 1);

            // Easing: Smooth start, accelerated arc, gentle dive
            const t = progress;

            // Quadratic Bezier Calculation
            const u = 1 - t;
            const currX = u * u * p0x + 2 * u * t * p1x + t * t * p2x;
            const currY = u * u * p0y + 2 * u * t * p1y + t * t * p2y;

            // 3D rotations & scaling
            const scale = 1 - (t * 0.82); // 1.0 down to 0.18
            const rotX = t * 360;         // 1 full somersault
            const rotY = t * 720;         // 2 full barrel rolls
            const rotZ = Math.sin(t * Math.PI) * 45; // tilt
            const transZ = Math.sin(t * Math.PI) * 160; // 3D depth pop towards viewer

            wrapper.style.transform = 
                "translate3d(" + (currX - cardW / 2) + "px, " + (currY - cardH / 2) + "px, " + transZ + "px) " +
                "scale(" + scale + ") " +
                "rotateX(" + rotX + "deg) " +
                "rotateY(" + rotY + "deg) " +
                "rotateZ(" + rotZ + "deg)";

            // Fade out near the destination
            if (t > 0.82) {
                wrapper.style.opacity = String((1 - t) / 0.18);
            }

            // Spawn particle trail
            if (elapsed - lastParticleTime > 40 && t < 0.95) {
                spawnParticle(currX, currY);
                lastParticleTime = elapsed;
            }

            if (progress < 1) {
                requestAnimationFrame(animate);
            } else {
                // Completed: Clean up flying card
                wrapper.remove();

                // 1. Ripple Effect on Target
                const ripple = document.createElement("div");
                ripple.className = "cart-impact-ripple";
                ripple.style.left = p2x + "px";
                ripple.style.top = p2y + "px";
                document.body.appendChild(ripple);
                setTimeout(function () { ripple.remove(); }, 700);

                // 2. Shake / Bounce Cart Icon
                targetCartEl.classList.remove("cart-impact-bounce");
                void targetCartEl.offsetWidth; // Trigger reflow
                targetCartEl.classList.add("cart-impact-bounce");
                setTimeout(function () {
                    targetCartEl.classList.remove("cart-impact-bounce");
                }, 700);

                // 3. Callback
                if (typeof callback === "function") {
                    callback();
                }
            }
        }

        requestAnimationFrame(animate);
    };

    // 11. SEARCH SUGGESTIONS PLACEHOLDER ROTATION
    const searchInput = document.querySelector("input[name='keyword']");
    if (searchInput && !searchInput.value) {
        const suggestions = [
            "Tim kem chong nang...",
            "Tim son moi...",
            "Tim sua rua mat...",
            "Tim toner...",
            "Tim serum...",
        ];
        let idx = 0;
        setInterval(function () {
            if (document.activeElement !== searchInput) {
                searchInput.setAttribute("placeholder", suggestions[idx % suggestions.length]);
                idx++;
            }
        }, 3000);
    }

    // Expose updateCartBadge globally
    window.updateCartBadge = updateCartBadge;
});