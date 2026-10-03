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
    focusFirst(dialog);

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

// showModal() ставит фокус на первый фокусируемый элемент — а это «Закрыть» в шапке. По правилам окна
// фокус идёт в первое поле тела; в окне без полей — на главную кнопку подвала. Явный autofocus
// (подтверждение удаления ставит его на «Отмену») побеждает.
// Скрытый input type=file (за кнопкой «Выбрать файл») — не поле: фокус в нём невидим.
const FIELD =
    ".cm-modal-body :is(input:not([type=hidden]):not([type=checkbox]):not([type=radio]):not([type=file]), select, textarea, [contenteditable=true]):not(:disabled):not([readonly])";

function focusFirst(dialog) {
    const field = dialog.querySelector("[autofocus]:not(:disabled)") ?? dialog.querySelector(FIELD);
    const target =
        field ??
        dialog.querySelector(".cm-modal-footer .cm-btn:is(.cm-btn-primary, .cm-btn-error):not(:disabled)") ??
        dialog.querySelector(".cm-modal-footer .cm-btn:not(:disabled)");

    if (target instanceof HTMLElement) {
        target.focus({ preventScroll: true });
    }

    // Тело, которое догружается (список сценариев, справочник навыков), рисует поля позже showModal(): фокус ушёл на
    // кнопку подвала. Ждём первое поле до 2 с и переводим фокус в него, если человек сам ничего не трогал.
    if (field === null) {
        const fallback = document.activeElement;
        const observer = new MutationObserver(() => {
            const late = dialog.querySelector(FIELD);
            if (late instanceof HTMLElement) {
                observer.disconnect();
                if (document.activeElement === fallback || document.activeElement === document.body) {
                    late.focus({ preventScroll: true });
                }
            }
        });
        observer.observe(dialog, { childList: true, subtree: true });
        setTimeout(() => observer.disconnect(), 2000);
    }
}
