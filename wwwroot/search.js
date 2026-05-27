// Global keyboard listener for search feature
let dotnetHelper = null;

window.addKeyboardListener = function (dotnetReference) {
    dotnetHelper = dotnetReference;

    document.addEventListener('keydown', function (e) {
        // Check for Windows key (Meta key)
        // Key code 91/92 are left/right Windows keys
        if (e.key === 'Meta' || e.code === 'OSLeft' || e.code === 'OSRight') {
            e.preventDefault();
            if (e.repeat) {
                return;
            }

            if (dotnetHelper) {
                dotnetHelper.invokeMethodAsync('OnStartKeyPressed');
            }
        }
    });
};

document.addEventListener('wheel', function (e) {
    const scrollRail = e.target.closest('.bottom-bar-windows, .bottom-bar-shortcuts');

    if (!scrollRail) {
        return;
    }

    const canScroll = scrollRail.scrollWidth > scrollRail.clientWidth;

    if (!canScroll) {
        return;
    }

    e.preventDefault();
    scrollRail.scrollLeft += e.deltaY || e.deltaX;
}, { passive: false });
