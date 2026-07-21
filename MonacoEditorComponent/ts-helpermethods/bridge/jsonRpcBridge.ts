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
    if (typeof (window as any).webkit !== 'undefined' &&
        (window as any).webkit.messageHandlers &&
        (window as any).webkit.messageHandlers.unoWebView &&
        typeof (window as any).webkit.messageHandlers.unoWebView.postMessage === 'function') {
        // macOS/Linux via WKWebView/WebKitGTK
        // Send as JSON string to maximize compatibility with native message handlers.
        (window as any).webkit.messageHandlers.unoWebView.postMessage(JSON.stringify(message));
    } else if (typeof (window as any).chrome !== 'undefined' &&
        (window as any).chrome.webview &&
        typeof (window as any).chrome.webview.postMessage === 'function') {
        // Windows WebView2
        (window as any).chrome.webview.postMessage(message);
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
    private _eventTargets: EventTarget[] = [];
    private _lastMessageFingerprint: string | null = null;
    private _lastMessageTimestamp = 0;

    constructor() {
        super();
    }

    public listen(callback: DataCallback): Disposable {
        this._callback = callback;

        this._messageListener = (event: MessageEvent) => {
            // Only process messages that look like JSON-RPC (have jsonrpc field or id field)
            let data = event.data;
            if (typeof data === 'string') {
                try {
                    data = JSON.parse(data);
                } catch {
                    return;
                }
            }
            if (data && typeof data === 'object' && (data.jsonrpc || data.id !== undefined)) {
                // When both window and chrome.webview channels are active, some hosts can
                // deliver the same envelope on both targets. Drop near-immediate duplicates.
                const fingerprint = JSON.stringify(data);
                const now = Date.now();
                if (this._lastMessageFingerprint === fingerprint && (now - this._lastMessageTimestamp) <= 5) {
                    return;
                }
                this._lastMessageFingerprint = fingerprint;
                this._lastMessageTimestamp = now;

                if (this._callback) {
                    this._callback(data as Message);
                }
            }
        };

        // The host (DesktopCodeEditorPresenter.PostWebMessageToPage) delivers host-to-page
        // JSON-RPC envelopes by dispatching a window 'message' event via ExecuteScript on
        // all platforms: CoreWebView2.PostWebMessageAsJson was confirmed (via CDP probe) to
        // never surface to the page on the WebView2 host we target, which hung every
        // request/response round-trip. So we MUST listen on window. We also listen on
        // chrome.webview defensively in case a host delivers there instead; the dedup guard
        // in the message listener drops any envelope that arrives on both channels.
        const chromeWebview = (window as any).chrome?.webview;
        if (chromeWebview && typeof chromeWebview.addEventListener === 'function') {
            chromeWebview.addEventListener('message', this._messageListener);
            this._eventTargets.push(chromeWebview);
        }
        window.addEventListener('message', this._messageListener);
        this._eventTargets.push(window);

        return {
            dispose: () => {
                if (this._messageListener) {
                    for (const target of this._eventTargets) {
                        target.removeEventListener('message', this._messageListener);
                    }
                    this._messageListener = null;
                    this._eventTargets = [];
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

/**
 * Returns the active JSON-RPC connection from window.__jsonRpc.
 * Throws a descriptive error if the bridge has not been initialized or has been disposed.
 * This is the single source of truth for connection acquisition -- all modules must use this
 * instead of accessing window.__jsonRpc directly.
 */
export function getConnection(): MessageConnection {
    const conn = (window as any).__jsonRpc as MessageConnection | undefined;
    if (!conn) {
        throw new Error(
            '[jsonRpcBridge] JSON-RPC connection is not available. ' +
            'The bridge has not been initialized or has been disposed.'
        );
    }
    return conn;
}

/**
 * Reference count for editors using the page-global JSON-RPC connection.
 * The connection is only disposed when the last editor releases it.
 */
let _connectionRefCount = 0;

/**
 * Increment the connection reference count. Called when an editor is created on desktop.
 */
export function retainConnection(): void {
    _connectionRefCount++;
}

/**
 * Decrement the connection reference count. Only disposes the page-global
 * JSON-RPC connection on a true 1 -> 0 transition. Unmatched release calls
 * (when count is already 0) are no-ops and do not dispose.
 * Returns true if the connection was actually disposed.
 */
export function releaseConnection(): boolean {
    if (_connectionRefCount <= 0) {
        // Already at zero -- no-op to prevent unmatched release from disposing
        return false;
    }
    _connectionRefCount--;
    if (_connectionRefCount === 0) {
        const conn = (window as any).__jsonRpc;
        if (conn) {
            conn.dispose();
            (window as any).__jsonRpc = undefined;
        }
        return true;
    }
    return false;
}

/**
 * Default timeout (ms) for init-time JSON-RPC requests (theme, property reads).
 * Prevents indefinite hangs if the C# host is slow or handlers are not yet registered.
 */
export const INIT_REQUEST_TIMEOUT_MS = 10000;

/**
 * Default timeout (ms) for runtime JSON-RPC requests (event callbacks, provider calls).
 * Longer than init timeout because provider callbacks can legitimately be slow under load.
 */
export const RUNTIME_REQUEST_TIMEOUT_MS = 30000;

/**
 * Wraps a JSON-RPC sendRequest with a timeout. Rejects with a descriptive error
 * if the response is not received within the given duration.
 */
export function sendRequestWithTimeout<T>(
    connection: MessageConnection,
    method: string,
    params: any,
    timeoutMs: number = INIT_REQUEST_TIMEOUT_MS
): Promise<T> {
    return new Promise<T>((resolve, reject) => {
        let settled = false;
        const timer = setTimeout(() => {
            if (!settled) {
                settled = true;
                reject(new Error(
                    `[jsonRpcBridge] JSON-RPC request '${method}' timed out after ${timeoutMs}ms`
                ));
            }
        }, timeoutMs);

        connection.sendRequest<T>(method, params).then(
            (result) => {
                if (!settled) {
                    settled = true;
                    clearTimeout(timer);
                    resolve(result);
                }
            },
            (error) => {
                if (!settled) {
                    settled = true;
                    clearTimeout(timer);
                    reject(error);
                }
            }
        );
    });
}
