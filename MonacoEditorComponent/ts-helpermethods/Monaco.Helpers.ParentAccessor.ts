class ParentAccessor {
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
        return ParentAccessor._managedGetJsonValue(this._managedOwner, name);
    }

    public callAction(name: string): boolean {
        return ParentAccessor._managedCallAction(this._managedOwner, name);
    }

    public callActionWithParameters(name: string, parameter1: string, parameter2: string): boolean {
        return ParentAccessor._managedCallActionWithParameters(this._managedOwner, name, [parameter1, parameter2]);
    }

    public callActionWithParameters2(name: string, parameters: string[]): boolean {
        return ParentAccessor._managedCallActionWithParameters(this._managedOwner, name, parameters);
    }

    public close(): void {
        ParentAccessor._managedClose(this._managedOwner);
    }

    //getChildValue(name: string, child: string): Promise<any>;
    //getJsonValue(name: string): Promise<string>;
    //getValue(name: string): Promise<any>;
    public async setValue(name: string, value: string): Promise<void> {
        ParentAccessor._managedSetValue(this._managedOwner, name, value);
    }

    public setValueWithType(name: string, value: string, type: string) {
        ParentAccessor._managedSetValueWithType(this._managedOwner, name, value, type);
    }

    //callActionWithParameters(name: string, parameter1: string, parameter2: string): boolean;
    public callEvent(name: string, parameter1: string, parameter2: string) {
        return ParentAccessor._managedCallEvent(this._managedOwner, name, [parameter1, parameter2]);
    }
}



////namespace Monaco.Helpers {
//    interface ParentAccessor {
//        callAction(name: string): boolean;
//        callActionWithParameters(name: string, parameters: string[]): boolean;
//        callEvent(name: string, parameters: string[]): Promise<string>
//        close();
//        getChildValue(name: string, child: string): Promise<any>;
//        getJsonValue(name: string): Promise<string>;
//        getValue(name: string): Promise<any>;
//        setValue(name: string, value: any): Promise<undefined>;
//        setValue(name: string, value: string, type: string): Promise<undefined>;
//        setValueWithType(name: string, value: string, type: string);
//        callActionWithParameters(name: string, parameter1: string, parameter2: string): boolean;
//        callEvent(name: string, callbackMethod: string, parameter1: string, parameter2: string);
//        getJsonValue(name: string, returnId: string);
//}

////}