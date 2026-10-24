/**
 * ========================================================
 * BEAUTYSHOP 3D DESIGN SYSTEM - CUSTOMER JS
 * Hero 3D interaction, Card tilt & Trình xem sản phẩm 3D 360°
 * ========================================================
 */

(function (window, document) {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        initHero3DInteraction();
        initProductCards3D();
        initProductDetail3DViewer();
    });

    /**
     * 1. Tương tác Hero 3D Bottle & Parallax
     */
    function initHero3DInteraction() {
        const heroBanner = document.getElementById("heroBanner");
        const bottle = document.querySelector(".hero-3d-bottle");
        const parallaxItems = document.querySelectorAll(".parallax-item");

        if (!heroBanner || !bottle || window.BeautyShop3D.prefersReducedMotion) return;

        let mouseX = 0, mouseY = 0;
        let isHover = false;
        let animId = null;

        function updateHero3D() {
            if (!isHover) return;

            const rect = heroBanner.getBoundingClientRect();
            const xNorm = (mouseX - rect.left) / rect.width - 0.5;
            const yNorm = (mouseY - rect.top) / rect.height - 0.5;

            // Xoay chai 3D theo chuột
            const rotX = (-yNorm * 22).toFixed(2);
            const rotY = (xNorm * 28).toFixed(2);
            bottle.style.transform = `rotateX(${rotX}deg) rotateY(${rotY}deg) scale(1.05)`;

            // Parallax các hạt và cánh hoa
            parallaxItems.forEach(function (item) {
                const depth = parseFloat(item.dataset.depth || "0.2");
                const moveX = (xNorm * depth * 80).toFixed(2);
                const moveY = (yNorm * depth * 80).toFixed(2);
                const rot = (xNorm * depth * 30).toFixed(2);
                item.style.transform = `translate3d(${moveX}px, ${moveY}px, 0) rotate(${rot}deg)`;
            });

            animId = null;
        }

        heroBanner.addEventListener("mouseenter", function () {
            isHover = true;
            bottle.style.animationPlayState = "paused";
        });

        heroBanner.addEventListener("mousemove", function (e) {
            mouseX = e.clientX;
            mouseY = e.clientY;
            if (!animId) {
                animId = requestAnimationFrame(updateHero3D);
            }
        });

        heroBanner.addEventListener("mouseleave", function () {
            isHover = false;
            if (animId) cancelAnimationFrame(animId);
            bottle.style.animationPlayState = "running";
            bottle.style.transform = "";
            parallaxItems.forEach(function (item) {
                item.style.transform = "";
            });
        });
    }

    /**
     * 2. Kích hoạt Tilt 3D cho các thẻ sản phẩm
     */
    function initProductCards3D() {
        if (!window.BeautyShop3D) return;
        window.BeautyShop3D.initTilt(".product-card, .product-item-card, .product-list-card", {
            maxTilt: 8,
            scale: 1.025,
            glare: true,
            maxGlare: 0.35
        });
    }

    /**
     * 3. Trình xem 3D xoay 360 độ trang Chi tiết sản phẩm
     */
    function initProductDetail3DViewer() {
        const toggleBtn2D = document.getElementById("btn-view-2d");
        const toggleBtn3D = document.getElementById("btn-view-3d");
        const staticPanel = document.getElementById("detail-static-panel");
        const viewer3DPanel = document.getElementById("detail-3d-panel");
        const canvasWrap = document.getElementById("product-3d-canvas-wrap");

        if (!toggleBtn3D || !viewer3DPanel) return;

        let threeLoaded = false;
        let isSpinning = true;
        let scene, camera, renderer, bottleMesh, animId;
        let isDragging = false;
        let previousMousePosition = { x: 0, y: 0 };

        // Nút chuyển chế độ 2D / 3D
        toggleBtn3D.addEventListener("click", function () {
            toggleBtn2D.classList.remove("active");
            toggleBtn3D.classList.add("active");
            staticPanel.style.display = "none";
            viewer3DPanel.style.display = "flex";

            if (!threeLoaded) {
                loadThreeJSAndInit();
            }
        });

        toggleBtn2D.addEventListener("click", function () {
            toggleBtn3D.classList.remove("active");
            toggleBtn2D.classList.add("active");
            viewer3DPanel.style.display = "none";
            staticPanel.style.display = "block";
        });

        function loadThreeJSAndInit() {
            // Hiển thị trạng thái đang tải
            const loadingIndicator = document.getElementById("viewer-3d-loading");
            if (loadingIndicator) loadingIndicator.style.display = "flex";

            if (window.THREE) {
                startThreeApp();
                return;
            }

            // Tải Three.js nhẹ qua CDN (lazy-load)
            const script = document.createElement("script");
            script.src = "https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js";
            script.onload = function () {
                threeLoaded = true;
                startThreeApp();
            };
            script.onerror = function () {
                // Dự phòng lỗi tải Three.js: hiện thông báo thân thiện
                if (loadingIndicator) {
                    loadingIndicator.innerHTML = '<div class="text-muted p-4 text-center"><i class="bi bi-exclamation-circle text-warning fs-3 mb-2"></i><br>Không thể tải trình xem 3D. Đang chuyển về ảnh tĩnh...</div>';
                    setTimeout(function () {
                        toggleBtn2D.click();
                    }, 2000);
                }
            };
            document.head.appendChild(script);
        }

        function startThreeApp() {
            const loadingIndicator = document.getElementById("viewer-3d-loading");
            if (loadingIndicator) loadingIndicator.style.display = "none";

            // Kiểm tra hỗ trợ WebGL
            try {
                const testCanvas = document.createElement("canvas");
                const gl = testCanvas.getContext("webgl") || testCanvas.getContext("experimental-webgl");
                if (!gl) throw new Error("WebGL not supported");
            } catch (e) {
                if (loadingIndicator) {
                    loadingIndicator.innerHTML = '<div class="text-muted p-4 text-center">Trình duyệt của bạn không hỗ trợ WebGL.</div>';
                    loadingIndicator.style.display = "flex";
                }
                return;
            }

            const width = canvasWrap.clientWidth || 400;
            const height = canvasWrap.clientHeight || 420;

            // Scene & Camera
            scene = new THREE.Scene();
            camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 1000);
            camera.position.set(0, 1.2, 5.5);

            // Renderer
            renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
            renderer.setSize(width, height);
            renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
            renderer.shadowMap.enabled = true;
            canvasWrap.appendChild(renderer.domElement);

            // Lighting: Luxury studio setup
            const ambientLight = new THREE.AmbientLight(0xffffff, 0.85);
            scene.add(ambientLight);

            const keyLight = new THREE.DirectionalLight(0xfff5ea, 1.2);
            keyLight.position.set(4, 8, 5);
            scene.add(keyLight);

            const fillLight = new THREE.DirectionalLight(0xe0f2fe, 0.6);
            fillLight.position.set(-4, -2, -3);
            scene.add(fillLight);

            const rimLight = new THREE.PointLight(0xf59e0b, 1.5, 10);
            rimLight.position.set(0, 3, -3);
            scene.add(rimLight);

            // Dựng mô hình 3D chai mỹ phẩm cao cấp bằng Geometry thuần (nhẹ và nhanh)
            const productGroup = new THREE.Group();

            // 1. Thân chai thủy tinh (Glass bottle body)
            const bottleGeo = new THREE.CylinderGeometry(0.85, 0.95, 2.6, 32);
            const bottleMat = new THREE.MeshPhysicalMaterial({
                color: 0x006E4A,
                metalness: 0.15,
                roughness: 0.1,
                transmission: 0.65,
                transparent: true,
                opacity: 0.92,
                reflectivity: 0.9,
                clearcoat: 1.0,
                clearcoatRoughness: 0.1
            });
            const bottleMeshObj = new THREE.Mesh(bottleGeo, bottleMat);
            bottleMeshObj.position.y = 0;
            productGroup.add(bottleMeshObj);

            // 2. Tinh chất bên trong (Serum core)
            const serumGeo = new THREE.CylinderGeometry(0.78, 0.85, 2.3, 32);
            const serumMat = new THREE.MeshStandardMaterial({
                color: 0xf59e0b,
                metalness: 0.2,
                roughness: 0.3,
                emissive: 0xd97706,
                emissiveIntensity: 0.25
            });
            const serumMesh = new THREE.Mesh(serumGeo, serumMat);
            productGroup.add(serumMesh);

            // 3. Nắp mạ vàng kim loại (Gold metallic cap)
            const capGeo = new THREE.CylinderGeometry(0.65, 0.65, 0.85, 32);
            const capMat = new THREE.MeshStandardMaterial({
                color: 0xf59e0b,
                metalness: 0.92,
                roughness: 0.25
            });
            const capMesh = new THREE.Mesh(capGeo, capMat);
            capMesh.position.y = 1.7;
            productGroup.add(capMesh);

            // 4. Nhãn chai mặt trước (Label)
            const labelGeo = new THREE.CylinderGeometry(0.86, 0.96, 1.2, 32, 1, true, 0, Math.PI * 0.9);
            const labelMat = new THREE.MeshStandardMaterial({
                color: 0xffffff,
                roughness: 0.4,
                side: THREE.DoubleSide
            });
            const labelMesh = new THREE.Mesh(labelGeo, labelMat);
            labelMesh.position.y = 0;
            labelMesh.rotation.y = Math.PI * 0.55;
            productGroup.add(labelMesh);

            // 5. Bệ tròn pha lê xoay (Pedestal)
            const pedGeo = new THREE.CylinderGeometry(1.6, 1.8, 0.15, 48);
            const pedMat = new THREE.MeshStandardMaterial({
                color: 0xffffff,
                metalness: 0.1,
                roughness: 0.2,
                transparent: true,
                opacity: 0.6
            });
            const pedMesh = new THREE.Mesh(pedGeo, pedMat);
            pedMesh.position.y = -1.35;
            scene.add(pedMesh);

            bottleMesh = productGroup;
            scene.add(bottleMesh);

            // Animation loop
            function animate() {
                animId = requestAnimationFrame(animate);

                if (isSpinning && !isDragging && bottleMesh) {
                    bottleMesh.rotation.y += 0.008;
                }

                renderer.render(scene, camera);
            }
            animate();

            // Xử lý kéo chuột để xoay 360 độ (Drag to rotate)
            canvasWrap.addEventListener("mousedown", function (e) {
                isDragging = true;
                previousMousePosition = { x: e.clientX, y: e.clientY };
            });

            window.addEventListener("mouseup", function () {
                isDragging = false;
            });

            window.addEventListener("mousemove", function (e) {
                if (!isDragging || !bottleMesh) return;

                const deltaMove = {
                    x: e.clientX - previousMousePosition.x,
                    y: e.clientY - previousMousePosition.y
                };

                bottleMesh.rotation.y += deltaMove.x * 0.012;
                bottleMesh.rotation.x += deltaMove.y * 0.008;
                bottleMesh.rotation.x = Math.max(-0.4, Math.min(0.4, bottleMesh.rotation.x));

                previousMousePosition = { x: e.clientX, y: e.clientY };
            });

            // Touch drag xoay trên điện thoại
            canvasWrap.addEventListener("touchstart", function (e) {
                if (e.touches.length === 1) {
                    isDragging = true;
                    previousMousePosition = { x: e.touches[0].clientX, y: e.touches[0].clientY };
                }
            }, { passive: true });

            window.addEventListener("touchend", function () {
                isDragging = false;
            });

            window.addEventListener("touchmove", function (e) {
                if (!isDragging || !bottleMesh || e.touches.length !== 1) return;

                const deltaMove = {
                    x: e.touches[0].clientX - previousMousePosition.x,
                    y: e.touches[0].clientY - previousMousePosition.y
                };

                bottleMesh.rotation.y += deltaMove.x * 0.015;
                bottleMesh.rotation.x += deltaMove.y * 0.01;
                bottleMesh.rotation.x = Math.max(-0.4, Math.min(0.4, bottleMesh.rotation.x));

                previousMousePosition = { x: e.touches[0].clientX, y: e.touches[0].clientY };
            }, { passive: true });

            // Các nút điều khiển
            const btnSpin = document.getElementById("btn-ctrl-spin");
            const btnZoomIn = document.getElementById("btn-ctrl-zoom-in");
            const btnZoomOut = document.getElementById("btn-ctrl-zoom-out");
            const btnReset = document.getElementById("btn-ctrl-reset");

            if (btnSpin) {
                btnSpin.addEventListener("click", function () {
                    isSpinning = !isSpinning;
                    this.classList.toggle("active", isSpinning);
                });
            }

            if (btnZoomIn) {
                btnZoomIn.addEventListener("click", function () {
                    if (camera && camera.position.z > 3.2) {
                        camera.position.z -= 0.6;
                    }
                });
            }

            if (btnZoomOut) {
                btnZoomOut.addEventListener("click", function () {
                    if (camera && camera.position.z < 8.5) {
                        camera.position.z += 0.6;
                    }
                });
            }

            if (btnReset) {
                btnReset.addEventListener("click", function () {
                    if (camera && bottleMesh) {
                        camera.position.set(0, 1.2, 5.5);
                        bottleMesh.rotation.set(0, 0, 0);
                    }
                });
            }

            // Hỗ trợ phím bàn phím (A11y keyboard controls)
            canvasWrap.setAttribute("tabindex", "0");
            canvasWrap.setAttribute("role", "region");
            canvasWrap.setAttribute("aria-label", "Trình xem mô hình 3D sản phẩm tương tác xoay 360 độ");

            canvasWrap.addEventListener("keydown", function (e) {
                if (!bottleMesh) return;
                if (e.key === "ArrowLeft") {
                    bottleMesh.rotation.y -= 0.1;
                } else if (e.key === "ArrowRight") {
                    bottleMesh.rotation.y += 0.1;
                } else if (e.key === "+" || e.key === "=") {
                    if (camera.position.z > 3.2) camera.position.z -= 0.5;
                } else if (e.key === "-" || e.key === "_") {
                    if (camera.position.z < 8.5) camera.position.z += 0.5;
                }
            });

            // Resize responsive
            window.addEventListener("resize", function () {
                if (!camera || !renderer || !canvasWrap) return;
                const newW = canvasWrap.clientWidth;
                const newH = canvasWrap.clientHeight;
                camera.aspect = newW / newH;
                camera.updateProjectionMatrix();
                renderer.setSize(newW, newH);
            });
        }
    }
})(window, document);
