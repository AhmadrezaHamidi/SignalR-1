// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

import { IHttpClient } from "../Microsoft.AspNetCore.SignalR.Client.TS/HttpClient"
import { HttpConnection } from "../Microsoft.AspNetCore.SignalR.Client.TS/HttpConnection"
import { IHttpConnectionOptions } from "../Microsoft.AspNetCore.SignalR.Client.TS/IHttpConnectionOptions"
import { DataReceived, TransportClosed } from "../Microsoft.AspNetCore.SignalR.Client.TS/Common"
import { ITransport, TransportType, TransferMode } from "../Microsoft.AspNetCore.SignalR.Client.TS/Transports"
import { eachTransport, eachEndpointUrl } from "./Common";

import { asyncit as it, captureException, PromiseSource, SyncPoint } from "./Utils";
import { connect } from "tls";

describe("Connection", () => {
    it("cannot be created with relative url if document object is not present", () => {
        expect(() => new HttpConnection("/test"))
            .toThrow(new Error("Cannot resolve '/test'."));
    });

    it("cannot be created with relative url if window object is not present", () => {
        (<any>global).window = {};
        expect(() => new HttpConnection("/test"))
            .toThrow(new Error("Cannot resolve '/test'."));
        delete (<any>global).window;
    });

    it("starting connection fails if getting id fails", async () => {
        let options: IHttpConnectionOptions = {
            httpClient: <IHttpClient>{
                post(url: string): Promise<string> {
                    return Promise.reject("error");
                },
                get(url: string): Promise<string> {
                    return Promise.resolve("");
                }
            },
            logging: null
        } as IHttpConnectionOptions;

        let connection = new HttpConnection("http://tempuri.org", options);

        let ex = await captureException(() => connection.start());
        expect(ex).not.toBeNull();
        expect(ex).toEqual("error");
    });

    it("cannot start a running connection", async () => {
        let options: IHttpConnectionOptions = {
            httpClient: <IHttpClient>{
                post(url: string): Promise<string> {
                    return Promise.resolve("{ \"connectionId\": \"42\", \"availableTransports\": [\"LongPolling\"] }");
                },
                get(url: string): Promise<string> {
                    return Promise.resolve("");
                }
            },
            transport: TransportType.LongPolling,
            logging: null
        };

        let connection = new HttpConnection("http://tempuri.org", options);

        // Start the connection
        await connection.start();

        // Try to start again, but it should fail
        let ex = await captureException(() => connection.start());
        expect(ex).not.toBeNull();
        expect(ex.message).toEqual("Cannot start a connection that is not in the 'Initial' state.");
    });

    it("cannot start a stopped connection", async () => {
        let options: IHttpConnectionOptions = {
            httpClient: <IHttpClient>{
                post(url: string): Promise<string> {
                    return Promise.resolve("{ \"connectionId\": \"42\", \"availableTransports\": [\"LongPolling\"] }");
                },
                get(url: string): Promise<string> {
                    return Promise.resolve("");
                }
            },
            transport: TransportType.LongPolling,
            logging: null
        };

        let connection = new HttpConnection("http://tempuri.org", options);

        let onClosed = new PromiseSource();
        connection.onclose = (e) => {
            if (e) {
                onClosed.reject(e)
            } else {
                onClosed.resolve();
            }
        };

        // Start
        await connection.start();

        // And then stop
        await connection.stop();
        await onClosed;

        // Try to start again, but it should fail (for now...)
        let ex = await captureException(() => connection.start());
        expect(ex).not.toBeNull();
        expect(ex.message).toBe("Cannot start a connection that is not in the 'Initial' state.")
    });

    it("can stop a starting connection", async () => {
        let negotiating = new SyncPoint();
        let options: IHttpConnectionOptions = {
            httpClient: <IHttpClient>{
                async post(url: string): Promise<string> {
                    await negotiating.waitToContinue();
                    return "{ \"connectionId\": \"42\", \"availableTransports\": [\"LongPolling\"] }";
                },
                get(url: string): Promise<string> {
                    return Promise.reject("should not have reached this point");
                }
            },
            logging: null
        };

        let connection = new HttpConnection("http://tempuri.org", options);

        // Start the connection
        await connection.start();

        // Wait for negotiation to begin
        await negotiating.waitForSyncPoint();

        // Stop the connection, and release the negotiating sync point
        let stopPromise = connection.stop();
        negotiating.continue();
        await stopPromise;
    });

    it("can stop a non-started connection", async () => {
        let connection = new HttpConnection("http://tempuri.org");
        await connection.stop();
    });

    it("preserves users connection string", async () => {
        let connectUrl: string;
        let fakeTransport: ITransport = {
            connect(url: string): Promise<TransferMode> {
                connectUrl = url;
                return Promise.reject(TransferMode.Text);
            },
            send(data: any): Promise<void> {
                return Promise.reject("");
            },
            stop(): void { },
            onreceive: undefined,
            onclose: undefined,
        }

        let options: IHttpConnectionOptions = {
            httpClient: <IHttpClient>{
                post(url: string): Promise<string> {
                    return Promise.resolve("{ \"connectionId\": \"42\" }");
                },
                get(url: string): Promise<string> {
                    return Promise.resolve("");
                }
            },
            transport: fakeTransport,
            logging: null
        } as IHttpConnectionOptions;


        let connection = new HttpConnection("http://tempuri.org?q=myData", options);

        await captureException(() => connection.start());

        expect(connectUrl).toBe("http://tempuri.org?q=myData&id=42");
    });

    eachEndpointUrl((givenUrl: string, expectedUrl: string) => {
        it("negotiate request puts 'negotiate' at the end of the path", async () => {
            let negotiateUrl: string;
            let connection: HttpConnection;
            let options: IHttpConnectionOptions = {
                httpClient: <IHttpClient>{
                    post(url: string): Promise<string> {
                        negotiateUrl = url;
                        connection.stop();
                        return Promise.resolve("{}");
                    },
                    get(url: string): Promise<string> {
                        connection.stop();
                        return Promise.resolve("");
                    }
                },
                logging: null
            };

            connection = new HttpConnection(givenUrl, options);

            await connection.start();

            expect(negotiateUrl).toBe(expectedUrl);
        });
    });

    eachTransport((requestedTransport: TransportType) => {
        // OPTIONS is not sent when WebSockets transport is explicitly requested
        if (requestedTransport === TransportType.WebSockets) {
            return;
        }
        it(`cannot be started if requested ${TransportType[requestedTransport]} transport not available on server`, async () => {
            let options: IHttpConnectionOptions = {
                httpClient: <IHttpClient>{
                    post(url: string): Promise<string> {
                        return Promise.resolve("{ \"connectionId\": \"42\", \"availableTransports\": [] }");
                    },
                    get(url: string): Promise<string> {
                        return Promise.resolve("");
                    }
                },
                transport: requestedTransport,
                logging: null
            };

            let connection = new HttpConnection("http://tempuri.org", options);

            let ex = await captureException(() => connection.start());
            expect(ex).not.toBeNull();
            expect(ex.message).toBe("No available transports found.");
        });
    });

    it("cannot be started if no transport available on server and no transport requested", async () => {
        let options: IHttpConnectionOptions = {
            httpClient: <IHttpClient>{
                post(url: string): Promise<string> {
                    return Promise.resolve("{ \"connectionId\": \"42\", \"availableTransports\": [] }");
                },
                get(url: string): Promise<string> {
                    return Promise.resolve("");
                }
            },
            logging: null
        };

        let connection = new HttpConnection("http://tempuri.org", options);
        let ex = await captureException(() => connection.start());
        expect(ex).not.toBeNull();
        expect(ex.message).toBe("No available transports found.");
    });

    it('does not send negotiate request if WebSockets transport requested explicitly', async () => {
        let options: IHttpConnectionOptions = {
            httpClient: <IHttpClient>{
                post(url: string): Promise<string> {
                    return Promise.reject("Should not be called");
                },
                get(url: string): Promise<string> {
                    return Promise.reject("Should not be called");
                }
            },
            transport: TransportType.WebSockets,
            logging: null
        };

        let connection = new HttpConnection("http://tempuri.org", options);

        let ex = await captureException(() => connection.start());
        expect(ex).not.toBeNull();
        expect(ex.message).toBe("WebSocket is not defined");
    });

    [
        [TransferMode.Text, TransferMode.Text],
        [TransferMode.Text, TransferMode.Binary],
        [TransferMode.Binary, TransferMode.Text],
        [TransferMode.Binary, TransferMode.Binary],
    ].forEach(([requestedTransferMode, transportTransferMode]) => {
        it(`connection returns ${transportTransferMode} transfer mode when ${requestedTransferMode} transfer mode is requested`, async () => {
            let fakeTransport = {
                // mode: TransferMode : TransferMode.Text
                connect(url: string, requestedTransferMode: TransferMode): Promise<TransferMode> { return Promise.resolve(transportTransferMode); },
                send(data: any): Promise<void> { return Promise.resolve(); },
                stop(): void { },
                onreceive: null,
                onclose: null,
                mode: transportTransferMode
            } as ITransport;

            let options: IHttpConnectionOptions = {
                httpClient: <IHttpClient>{
                    post(url: string): Promise<string> {
                        return Promise.resolve("{ \"connectionId\": \"42\", \"availableTransports\": [] }");
                    },
                    get(url: string): Promise<string> {
                        return Promise.resolve("");
                    }
                },
                transport: fakeTransport,
                logging: null
            } as IHttpConnectionOptions;

            let connection = new HttpConnection("https://tempuri.org", options);
            connection.features.transferMode = requestedTransferMode;
            await connection.start();
            let actualTransferMode = connection.features.transferMode;

            expect(actualTransferMode).toBe(transportTransferMode);
        });
    });
});
