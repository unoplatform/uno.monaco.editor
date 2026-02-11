import { isDesktopHost, getConnection, sendRequestWithTimeout } from './bridge/jsonRpcBridge';

/**
 * Module-level flag: true when running in a WebView2/WKWebView host (desktop),
 * false when running under Uno WASM Bootstrap (browser).
 */
const _isDesktop: boolean = isDesktopHost();

export class ParentAccessor {
    private _managedOwner: any;
    private static _managedGetJsonValue: (managedOwner: any, name: string) => string;
    private static _managedCallAction: (managedOwner: any, name: string) => boolean;
    private static _managedCallActionWithParameters: (managedOwner: any, name: string, parameters: string[]) => boolean;
    private static _managedCallEvent: (managedOwner: any, name: string, parameters: string[]) => Promise<string>;
    private static _managedClose: (managedOwner: any) => void;
    private static _managedSetValue: (managedOwner: any, name: string, value: string) => void;
    private static _managedSetValueWithType: (managedOwner: any, name: string, value: string, type: string) => void;

    constructor(managedOwner: any) {
        this._managedOwner = managedOwner;
    }

    public static async setup() {
        if (_isDesktop) {
            // No JSExport setup needed on desktop -- JSON-RPC bridge handles everything
            return;
        }

        let anyModule = (<any>window).Module;

        if (anyModule.getAssemblyExports !== undefined) {
            const browserExports = await anyModule.getAssemblyExports("MonacoEditorComponent");

            ParentAccessor._managedGetJsonValue = browserExports.Monaco.Helpers.ParentAccessor.ManagedGetJsonValue;
            ParentAccessor._managedCallAction = browserExports.Monaco.Helpers.ParentAccessor.ManagedCallAction;
            ParentAccessor._managedCallActionWithParameters = browserExports.Monaco.Helpers.ParentAccessor.ManagedCallActionWithParameters;
            ParentAccessor._managedCallEvent = browserExports.Monaco.Helpers.ParentAccessor.ManagedCallEvent;
            ParentAccessor._managedClose = browserExports.Monaco.Helpers.ParentAccessor.ManagedClose;
            ParentAccessor._managedSetValue = browserExports.Monaco.Helpers.ParentAccessor.ManagedSetValue;
            ParentAccessor._managedSetValueWithType = browserExports.Monaco.Helpers.ParentAccessor.ManagedSetValueWithType;
        }
    }

    public getJsonValue(name: string): string {
        if (_isDesktop) {
            // Sync path should not be called on desktop; callers must use getJsonValueAsync
            throw new Error('ParentAccessor.getJsonValue is not available on desktop. Use getJsonValueAsync instead.');
        }
        return ParentAccessor._managedGetJsonValue(this._managedOwner, name);
    }

    public async getJsonValueAsync(name: string): Promise<string> {
        if (_isDesktop) {
            return await sendRequestWithTimeout<string>(
                getConnection(), 'parentAccessor/getJsonValue', { name }
            );
        }
        return ParentAccessor._managedGetJsonValue(this._managedOwner, name);
    }

    public callAction(name: string): boolean | void {
        if (_isDesktop) {
            getConnection().sendNotification('parentAccessor/callAction', { name });
            return;
        }
        return ParentAccessor._managedCallAction(this._managedOwner, name);
    }

    public callActionWithParameters(name: string, parameter1: string, parameter2: string): boolean | void {
        if (_isDesktop) {
            getConnection().sendNotification('parentAccessor/callActionWithParameters', {
                name,
                parameters: [parameter1, parameter2]
            });
            return;
        }
        return ParentAccessor._managedCallActionWithParameters(this._managedOwner, name, [parameter1, parameter2]);
    }

    public callActionWithParameters2(name: string, parameters: string[]): boolean | void {
        if (_isDesktop) {
            getConnection().sendNotification('parentAccessor/callActionWithParameters', {
                name,
                parameters
            });
            return;
        }
        return ParentAccessor._managedCallActionWithParameters(this._managedOwner, name, parameters);
    }

    public close(): void {
        if (_isDesktop) {
            // Dispose the JSON-RPC connection -- rejects pending requests and removes listeners
            const conn = (window as any).__jsonRpc;
            if (conn) {
                conn.dispose();
                (window as any).__jsonRpc = undefined;
            }
            return;
        }
        ParentAccessor._managedClose(this._managedOwner);
    }

    public async setValue(name: string, value: string): Promise<void> {
        if (_isDesktop) {
            getConnection().sendNotification('parentAccessor/setValue', { name, value });
            return;
        }
        ParentAccessor._managedSetValue(this._managedOwner, name, value);
    }

    public setValueWithType(name: string, value: string, type: string) {
        if (_isDesktop) {
            getConnection().sendNotification('parentAccessor/setValueWithType', { name, value, typeName: type });
            return;
        }
        ParentAccessor._managedSetValueWithType(this._managedOwner, name, value, type);
    }

    public async callEvent(name: string, parameter1: string, parameter2: string): Promise<string> {
        if (_isDesktop) {
            return await sendRequestWithTimeout<string>(
                getConnection(), 'parentAccessor/callEvent', {
                    name,
                    parameters: [parameter1, parameter2]
                }
            );
        }
        return ParentAccessor._managedCallEvent(this._managedOwner, name, [parameter1, parameter2]);
    }
}
