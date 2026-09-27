// Smart Laundry - SignalR Real-Time Status Updates & CSRF Token Helper
(() => {
    // 1. Maintain CSRF Token helper
    const tokenMeta = document.querySelector('input[name="__RequestVerificationToken"]');
    window.__csrf = tokenMeta ? tokenMeta.value : '';

    // 2. Initialize SignalR Connection if library is available
    if (typeof signalR !== 'undefined' && document.querySelector('[id^="order-badge-"]')) {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/orders")
            .withAutomaticReconnect()
            .build();

        connection.on("OrderStatusChanged", (orderId, newStatus) => {
            console.log(`[SignalR] Order #${orderId} changed status to: ${newStatus}`);

            // Update badge on Customer or Staff view if present
            const badge = document.getElementById(`order-badge-${orderId}`);
            if (badge) {
                // Strip previous stage- classes
                const classes = Array.from(badge.classList).filter(c => !c.startsWith('stage-'));
                classes.push(`stage-${newStatus}`);
                badge.className = classes.join(' ');
                badge.textContent = newStatus;

                // Add highlight pulse animation
                badge.style.transition = 'transform 0.2s ease';
                badge.style.transform = 'scale(1.25)';
                setTimeout(() => badge.style.transform = 'scale(1)', 300);
            }

            // Create floating Toast notification
            showToast(`Order #${orderId} status updated to: ${newStatus}`);
        });

        connection.start()
            .then(() => {
                console.log("[SignalR] Connected to /hubs/orders");
                if (window.__customerId) {
                    connection.invoke("JoinGroup", window.__customerId);
                }
            })
            .catch(err => console.error("[SignalR] Connection error:", err));
    }

    function showToast(message) {
        let toastContainer = document.getElementById('toast-container');
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.id = 'toast-container';
            toastContainer.style.cssText = 'position: fixed; bottom: 20px; right: 20px; z-index: 9999; display: flex; flex-direction: column; gap: 8px;';
            document.body.appendChild(toastContainer);
        }

        const toast = document.createElement('div');
        toast.style.cssText = 'background: #1e1b4b; color: #fff; padding: 12px 18px; border-radius: 10px; font-weight: 600; font-size: 0.9rem; box-shadow: 0 10px 25px rgba(0,0,0,0.2); opacity: 0; transform: translateY(10px); transition: all 0.3s ease; display: flex; align-items: center; gap: 8px;';
        toast.innerHTML = `<span>🔔</span> ${message}`;

        toastContainer.appendChild(toast);

        requestAnimationFrame(() => {
            toast.style.opacity = '1';
            toast.style.transform = 'translateY(0)';
        });

        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateY(10px)';
            setTimeout(() => toast.remove(), 300);
        }, 4000);
    }
})();