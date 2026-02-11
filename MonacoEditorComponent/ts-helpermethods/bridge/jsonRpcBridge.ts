import {
    AbstractMessageReader,
    AbstractMessageWriter,
    createMessageConnection,
    DataCallback,
    Disposable,
    Message,
    MessageConnection,
    MessageReader,
    MessageWriter
} from 'vscode-jsonrpc/browser';

/**
 * Sends messages to the host via platform-specific postMessage.
 * Windows: chrome.webview.postMessage
 * macOS/Linux: webkit.messageHandlers.unoWebView.postMessage
 */
function postWebViewMessage(message: any): void {
    if (typeof (window as any).chrome !== 'undefined' &&
        (window as any).chrome.webview &&
        typeof (window as any).chrome.webview.postMessage === 'function') {
        // Windows WebView2
        (window as any).chrome.webview.postMessage(message);
    } else if (typeof (window as any).webkit !== 'undefined' &&
        (window as any).webkit.messageHandlers &&
        (window as any).webkit.messageHandlers.unoWebView &&
        typeof (window as any).webkit.messageHandlers.unoWebView.postMessage === 'function') {
        // macOS/Linux via WKWebView/WebKitGTK
        (window as any).webkit.messageHandlers.unoWebView.postMessage(message);
    } else {
        console.warn('[jsonRpcBridge] No WebView message transport available');
    }
}

/**
 * Reads JSON-RPC messages from the WebView host.
 * On Windows, CoreWebView2.PostWebMessageAsJson() fires 'message' on chrome.webview.
 * On macOS/Linux (WKWebView/WebKitGTK), messages arrive via window 'message'.
 */
class WebViewMessageReader extends AbstractMessageReader implements MessageReader {
    private _callback: DataCallback | null = null;
    private _messageListener: ((event: MessageEvent) => void) | null = null;
    private _eventTarget: EventTarget | null = null;

    constructor() {
        super();
    }

    public listen(callback: DataCallback): Disposable {
        this._callback = callback;

        this._messageListener = (event: MessageEvent) => {
            // Only process messages that look like JSON-RPC (have jsonrpc field or id field)
            const data = event.data;
            if (data && typeof data === 'object' && (data.jsonrpc || data.id !== undefined)) {
                if (this._callback) {
                    this._callback(data as Message);
                }
            }
        };

        // On Windows, WebView2 delivers host-to-page messages via chrome.webview 'message' events,
        // NOT window 'message'. Subscribe to the correct target per platform.
        // On macOS/Linux (WKWebView/WebKitGTK), messages arrive via window 'message'.
        const chromeWebview = (window as any).chrome?.webview;
        if (chromeWebview && typeof chromeWebview.addEventListener === 'function') {
            chromeWebview.addEventListener('message', this._messageListener);
        } else {
            window.addEventListener('message', this._messageListener);
        }
        this._eventTarget = chromeWebview || window;

        return {
            dispose: () => {
                if (this._messageListener && this._eventTarget) {
                    this._eventTarget.removeEventListener('message', this._messageListener);
                    this._messageListener = null;
                    this._eventTarget = null;
                }
                this._callback = null;
            }
        };
    }
}

/**
 * Writes JSON-RPC messages to the WebView host via platform-specific postMessage.
 */
class WebViewMessageWriter extends AbstractMessageWriter implements MessageWriter {
    constructor() {
        super();
    }

    public async write(msg: Message): Promise<void> {
        try {
            postWebViewMessage(msg);
        } catch (error) {
            this.fireError(error, msg, undefined);
            throw error;
        }
    }

    public end(): void {
        // No-op: WebView message transport does not need explicit close
    }
}

/**
 * Whether the bridge is available (running in a WebView2/WKWebView host).
 */
export function isDesktopHost(): boolean {
    return (
        (typeof (window as any).chrome !== 'undefined' &&
            !!(window as any).chrome?.webview?.postMessage) ||
        (typeof (window as any).webkit !== 'undefined' &&
            !!(window as any).webkit?.messageHandlers?.unoWebView?.postMessage)
    );
}

/**
 * Creates and returns a vscode-jsonrpc MessageConnection wired to the WebView2 postMessage transport.
 * Assigns the connection to window.__jsonRpc for external access (Playwright tests, etc.).
 */
export function createBridgeConnection(): MessageConnection {
    const reader = new WebViewMessageReader();
    const writer = new WebViewMessageWriter();
    const connection = createMessageConnection(reader, writer);

    // Expose on window for external access (Playwright tests, Task 4/5)
    (window as any).__jsonRpc = connection;

    return connection;
}
