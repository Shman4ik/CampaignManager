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

/**
 * Downloads a file from byte array
 * @param {Uint8Array} data - The file data
 * @param {string} fileName - The name for the downloaded file
 * @param {string} contentType - The MIME type of the file
 */
window.downloadFileFromByteArray = function(data, fileName, contentType) {
    const blob = new Blob([new Uint8Array(data)], { type: contentType });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.style.display = 'none';
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
};
