// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

import { clearTimeout, setTimeout } from "timers";

export function asyncit(expectation: string, assertion?: () => Promise<any> | void, timeout?: number): void {
    let testFunction: (done: DoneFn) => void;
    if (assertion) {
        testFunction = done => {
            let result = assertion();
            if (result) {
                // Asynchronous test
                result.then(() => done())
                    .catch((err) => {
                        fail(err);
                        done();
                    });
            } else {
                // Synchronous test
                done();
            }
        };
    }

    it(expectation, testFunction, timeout);
}

export async function captureException(fn: () => Promise<any>): Promise<Error> {
    try {
        await fn();
        return null;
    } catch (e) {
        return e;
    }
}

export function delay(durationInMilliseconds: number): Promise<void> {
    let source = new PromiseSource<void>();
    setTimeout(() => source.resolve(), durationInMilliseconds);
    return source.promise;
}

export class PromiseSource<T> {
    public promise: Promise<T>

    private resolver: (value?: T | PromiseLike<T>) => void;
    private rejecter: (reason?: any) => void;

    constructor() {
        this.promise = new Promise<T>((resolve, reject) => {
            this.resolver = resolve;
            this.rejecter = reject;
        });
    }

    resolve(value?: T | PromiseLike<T>) {
        this.resolver(value);
    }

    reject(reason?: any) {
        this.rejecter(reason);
    }
}

export class SyncPoint {
    private _atSyncPoint: PromiseSource<void>;
    private _continueFromSyncPoint: PromiseSource<void>;

    async waitToContinue(): Promise<void> {
        this._atSyncPoint.resolve();
        await this._continueFromSyncPoint.promise;
    }

    waitForSyncPoint(): Promise<void> {
        return this._atSyncPoint.promise;
    }

    continue() {
        this._continueFromSyncPoint.resolve();
    }
}
