// Script editor utilities
window.setupTabHandler = function (textareaElement) {
    if (!textareaElement) return;
    
    textareaElement.addEventListener('keydown', function (e) {
        if (e.key === 'Tab') {
            e.preventDefault();
            
            var start = this.selectionStart;
            var end = this.selectionEnd;
            var value = this.value;
            
            if (e.shiftKey) {
                // Shift+Tab: remove indentation
                var lineStart = value.lastIndexOf('\n', start - 1) + 1;
                var lineText = value.substring(lineStart, start);
                if (lineText.startsWith('  ')) {
                    this.value = value.substring(0, lineStart) + value.substring(lineStart + 2);
                    this.selectionStart = this.selectionEnd = start - 2;
                }
            } else {
                // Tab: insert 2 spaces
                this.value = value.substring(0, start) + '  ' + value.substring(end);
                this.selectionStart = this.selectionEnd = start + 2;
            }
            
            // Trigger input event so Blazor picks up the change
            this.dispatchEvent(new Event('input', { bubbles: true }));
        }
    });
};
