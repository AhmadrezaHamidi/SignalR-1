
enum ConnectionState {
    Disconnected,
    Connecting,
    Connected
}

class Connection {
    private connectionState: ConnectionState;
    private url: string;
    private queryString: string;
    private connectionId: string;
    private transport: ITransport;
    private dataReceivedCallback: DataReceived = (data: any) => { };
    private connectionClosedCallback: ConnectionClosed = (error?: any) => { };

    constructor(url: string, queryString: string = "") {
        this.url = url;
        this.queryString = queryString;
        this.connectionState = ConnectionState.Disconnected;
    }

    async start(transportNames?: string[]): Promise<void> {
        if (this.connectionState != ConnectionState.Disconnected) {
            throw new Error("Cannot start a connection that is not in the 'Disconnected' state");
        }

        let transports = this.filterTransports(transportNames);
        if (transports.length == 0) {
            throw new Error("No valid transports requested.");
        }

        this.connectionId = await new HttpClient().get(`${this.url}/getid?${this.queryString}`);
        this.queryString = `id=${this.connectionId}`;
        
        try {
            this.transport = await this.tryStartTransport(transports);
            this.connectionState = ConnectionState.Connected;
        }
        catch (e) {
            console.log("Failed to start the connection.")
            this.connectionState = ConnectionState.Disconnected;
            throw e;
        }
    }

    private filterTransports(transportNames: string[]): ITransport[] {
        let availableTransports = ['webSockets', 'serverSentEvents', 'longPolling'];
        transportNames = transportNames || availableTransports;
        // uniquify
        transportNames = transportNames.filter((value, index, values) => {
            return values.indexOf(value) == index;
        });

        let transports: ITransport[] = [];
        transportNames.forEach(transportName => {
            if (transportName === 'webSockets') {
                transports.push(new WebSocketTransport());
            }
            if (transportName === 'serverSentEvents') {
                transports.push(new ServerSentEventsTransport());
            }
            if (transportName === 'longPolling') {
                transports.push(new LongPollingTransport());
            }
        });

        return transports;
    }

    private async tryStartTransport(transports: ITransport[]): Promise<ITransport> {
        let thisConnection = this;
        for (var index = 0; index < transports.length;) {
            var transport = transports[index];
            transport.onDataReceived = data => thisConnection.dataReceivedCallback(data);
            transport.onError = e => thisConnection.stopConnection(e);

            try {
                await transport.connect(this.url, this.queryString);
                return transport;
            }
            catch (ex) {
                index++;
                if (index >= transports.length) {
                    throw new Error('No transport could be started.')
                }
            }
        }
    }

    async send(data: any): Promise<void> {
        if (this.connectionState != ConnectionState.Connected) {
            throw new Error("Cannot send data if the connection is not in the 'Connected' State");
        }
        await this.transport.send(data);
    }

    stop(): void {
        if (this.connectionState != ConnectionState.Connected) {
            throw new Error("Cannot stop the connection if it is not in the 'Connected' State");
        }

        this.stopConnection();
    }

    private stopConnection(error?: any) {
        this.transport.stop();
        this.connectionState = ConnectionState.Disconnected;
        this.connectionClosedCallback(error);
    }

    set dataReceived(callback: DataReceived) {
        this.dataReceivedCallback = callback;
    }

    set connectionClosed(callback: ConnectionClosed) {
        this.connectionClosedCallback = callback;
    }
}