/**
 * PharmaCare Premium Interactions
 * Handles global UI behaviors, sidebar toggling, and animations
 */

document.addEventListener('DOMContentLoaded', () => {
    // 1. Sidebar Toggle Logic
    const sidebar = document.getElementById('sidebar');
    const sidebarToggle = document.getElementById('sidebarToggle');
    const closeSidebarBtn = document.getElementById('closeSidebarBtn');
    const sidebarOverlay = document.getElementById('sidebarOverlay');
    const html = document.documentElement;

    if (sidebarToggle && sidebar) {
        // Load saved state (only apply on desktop)
        const isCollapsed = localStorage.getItem('sidebar-collapsed') === 'true';
        if (isCollapsed && window.innerWidth >= 992) {
            html.classList.add('sidebar-collapsed');
        }

        sidebarToggle.addEventListener('click', () => {
            if (window.innerWidth >= 992) {
                // Desktop: Toggle collapsed state
                html.classList.toggle('sidebar-collapsed');
                localStorage.setItem('sidebar-collapsed', html.classList.contains('sidebar-collapsed'));
            } else {
                // Mobile: Slide in sidebar
                sidebar.classList.add('show');
                if (sidebarOverlay) sidebarOverlay.classList.add('show');
            }
        });
    }

    if (closeSidebarBtn && sidebar) {
        closeSidebarBtn.addEventListener('click', () => {
            sidebar.classList.remove('show');
            if (sidebarOverlay) sidebarOverlay.classList.remove('show');
        });
    }

    if (sidebarOverlay && sidebar) {
        sidebarOverlay.addEventListener('click', () => {
            sidebar.classList.remove('show');
            sidebarOverlay.classList.remove('show');
        });
    }

    // 2. Theme Toggle Logic
    const themeToggle = document.getElementById('themeToggle');
    if (themeToggle) {
        // Initialize theme: Default to 'dark'
        const savedTheme = localStorage.getItem('theme') || 'dark';
        html.setAttribute('data-theme', savedTheme);

        themeToggle.addEventListener('click', () => {
            const currentTheme = html.getAttribute('data-theme') || 'dark';
            const newTheme = currentTheme === 'dark' ? 'light' : 'dark';

            html.setAttribute('data-theme', newTheme);
            localStorage.setItem('theme', newTheme);
        });
    }

    // 3. Entrance Animation
    document.body.classList.add('page-loaded');

    // 4. Hover effects for stat cards to create a subtle glow tracking mouse (optional micro-interaction)
    const cards = document.querySelectorAll('.stat-card-modern, .premium-report-card');
    cards.forEach(card => {
        card.addEventListener('mousemove', e => {
            const rect = card.getBoundingClientRect();
            const x = e.clientX - rect.left;
            const y = e.clientY - rect.top;
            card.style.setProperty('--mouse-x', `${x}px`);
            card.style.setProperty('--mouse-y', `${y}px`);
        });
    });

    // 5. Scroll to Top Logic
    const scrollToTopBtn = document.getElementById('scrollToTopBtn');
    if (scrollToTopBtn) {
        window.addEventListener('scroll', () => {
            if (window.scrollY > 300) {
                scrollToTopBtn.classList.add('show');
            } else {
                scrollToTopBtn.classList.remove('show');
            }
        });

        scrollToTopBtn.addEventListener('click', () => {
            window.scrollTo({
                top: 0,
                behavior: 'smooth'
            });
        });
    }
});
