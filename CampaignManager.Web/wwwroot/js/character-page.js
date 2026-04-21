// Character page utilities

/**
 * Smoothly scrolls to a section with offset for sticky header
 * @param {string} elementId - The ID of the element to scroll to
 */
window.scrollToElement = function(elementId) {
    const element = document.getElementById(elementId);
    if (!element) return;

    // Measure the page-topbar height (shared sticky header component)
    let totalOffset = 0;
    const topbar = document.querySelector('.page-topbar');
    if (topbar) {
        totalOffset += topbar.offsetHeight;
    }
    totalOffset += 8; // small padding

    const elementTop = element.getBoundingClientRect().top + window.pageYOffset;
    window.scrollTo({
        top: elementTop - totalOffset,
        behavior: 'smooth'
    });
};

