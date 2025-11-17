// Character page utilities

/**
 * Smoothly scrolls to a section with offset for sticky header
 * @param {string} elementId - The ID of the element to scroll to
 */
window.scrollToElement = function(elementId) {
    console.log('scrollToElement called with:', elementId);
    
    const element = document.getElementById(elementId);
    console.log('Element found:', element);
    
    if (element) {
        const headerOffset = 140; // Increased offset for sticky header + топбар
        
        // Используем scrollIntoView с последующей коррекцией
        element.scrollIntoView({ 
            behavior: 'smooth', 
            block: 'start'
        });
        
        // Даем время для начала анимации, затем корректируем позицию
        setTimeout(() => {
            const currentScrollY = window.pageYOffset || document.documentElement.scrollTop;
            const targetScrollY = currentScrollY - headerOffset;
            
            window.scrollTo({
                top: targetScrollY,
                behavior: 'smooth'
            });
        }, 10);
    } else {
        console.warn('Element not found:', elementId);
    }
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
