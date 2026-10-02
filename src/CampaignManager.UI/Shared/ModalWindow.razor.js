// Нативный <dialog> для Modal: showModal() даёт верхний слой, фокус внутрь и Esc.
// Закрывать окно решает .NET (Open = false убирает его из дерева), поэтому Esc и касание
// подложки только просят: событие cancel отменяется, вызывается RequestClose.

let openCount = 0;

export function show(dialog, dotnet, dismissible) {
    const state = { dismissible, released: false };
    const previousFocus = document.activeElement;

    const onCancel = (event) => {
        event.preventDefault();
        if (state.dismissible) {
            dotnet.invokeMethodAsync("RequestClose");
        }
    };

    // У окна нет внутренних отступов, поэтому цель-сам-dialog — это касание подложки.
    const onClick = (event) => {
        if (event.target === dialog && state.dismissible) {
            dotnet.invokeMethodAsync("RequestClose");
        }
    };

    // Chrome не даёт отменить второй Esc подряд и закрывает окно сам. Окно, которое .NET ещё
    // считает открытым, либо закрываем через .NET, либо (если закрывать нельзя) открываем снова.
    const onClose = () => {
        if (state.released) {
            return;
        }
        if (state.dismissible) {
            dotnet.invokeMethodAsync("RequestClose");
        } else if (dialog.isConnected) {
            dialog.showModal();
        }
    };

    dialog.addEventListener("cancel", onCancel);
    dialog.addEventListener("click", onClick);
    dialog.addEventListener("close", onClose);
    dialog.showModal();

    openCount++;
    document.documentElement.classList.add("cm-modal-open");

    return {
        setDismissible(value) {
            state.dismissible = value;
        },
        release() {
            if (state.released) {
                return;
            }
            state.released = true;
            dialog.removeEventListener("cancel", onCancel);
            dialog.removeEventListener("click", onClick);
            dialog.removeEventListener("close", onClose);
            if (dialog.open) {
                dialog.close();
            }

            openCount = Math.max(0, openCount - 1);
            if (openCount === 0) {
                document.documentElement.classList.remove("cm-modal-open");
            }

            if (previousFocus instanceof HTMLElement && previousFocus.isConnected) {
                previousFocus.focus({ preventScroll: true });
            }
        },
    };
}
