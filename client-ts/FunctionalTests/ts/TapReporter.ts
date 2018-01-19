import { ILogger, LogLevel } from '@aspnet/signalr';

export class TapReporter implements jasmine.CustomReporter, ILogger {
    static Default: TapReporter = new TapReporter(console.log);

    constructor(private output: (message?: any, ...optionalParams: any[]) => void) {
    }

    public jasmineStarted(suiteInfo: jasmine.SuiteInfo) {
        this.output("TAP version 13");
        this.output(`1..${suiteInfo.totalSpecsDefined}`);
    }

    public suiteStarted(result: jasmine.CustomReporterResult) {

    }

    public specStarted(result: jasmine.CustomReporterResult) {
        
    }
    
    public specDone(result: jasmine.CustomReporterResult) {
        
    }

    public suiteDone(result: jasmine.CustomReporterResult) {
        
    }

    public jasmineDone(runDetails: jasmine.RunDetails) {
        
    }

    public log(logLevel: LogLevel, message: string) {

    }
}

// Suppress console.log output, and send it to the reporter instead
jasmine.getEnv().addReporter(TapReporter.Default);