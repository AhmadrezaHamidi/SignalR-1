// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

import { HttpError, TimeoutError } from "./Errors"

export interface IHttpClient {
    supportsBinary: boolean;
    send(method: string, url: string, content?: string, options?: HttpRequestOptions): Promise<HttpResponse>;
}

export interface HttpResponse {
    status: number,
    content: string | ArrayBuffer
}

export interface HttpRequestOptions {
    headers?: Map<string, string>,
    responseType?: XMLHttpRequestResponseType,
    timeout?: number,
}

export class HttpClient implements IHttpClient {
    supportsBinary: boolean;

    constructor() {
        this.supportsBinary = (typeof new XMLHttpRequest().responseType !== "string");
    }

    send(method: string, url: string, content?: string, options?: HttpRequestOptions): Promise<HttpResponse> {
        return new Promise<HttpResponse>((resolve, reject) => {
            options = options || {};
            let xhr = new XMLHttpRequest();

            xhr.open(method, url, true);
            xhr.setRequestHeader("X-Requested-With", "XMLHttpRequest");

            if (options.responseType) {
                xhr.responseType = options.responseType;
            }

            if (options.headers) {
                options.headers.forEach((value, header) => xhr.setRequestHeader(header, value));
            }

            xhr.onload = () => {
                if (xhr.status >= 200 && xhr.status < 300) {
                    resolve({
                        status: xhr.status,
                        content: xhr.response || xhr.responseText
                    })
                }
                else {
                    reject(new HttpError(xhr.statusText, xhr.status));
                }
            };

            xhr.onerror = () => {
                reject(new HttpError(xhr.statusText, xhr.status));
            }

            xhr.ontimeout = () => {
                reject(new TimeoutError("The timeout period elapsed"));
            }

            xhr.send(content);
        });
    }
}
