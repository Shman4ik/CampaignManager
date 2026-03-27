// Character page utilities

/**
 * Smoothly scrolls to a section with offset for sticky header
 * @param {string} elementId - The ID of the element to scroll to
 */
window.scrollToElement = function(elementId) {
    const element = document.getElementById(elementId);
    if (!element) return;

    // Dynamically measure sticky headers to get the correct offset
    const stickyBars = document.querySelectorAll('.sticky');
    let totalOffset = 0;
    stickyBars.forEach(function(bar) {
        const pos = window.getComputedStyle(bar).position;
        if (pos === 'sticky' || pos === 'fixed') {
            totalOffset += bar.offsetHeight;
        }
    });
    totalOffset += 8; // small padding

    const elementTop = element.getBoundingClientRect().top + window.pageYOffset;
    window.scrollTo({
        top: elementTop - totalOffset,
        behavior: 'smooth'
    });
};

