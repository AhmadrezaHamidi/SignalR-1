import { Selector } from 'testcafe'

const jasmineBar = Selector('.jasmine-bar', { timeout: 60 * 1000 });
const jasmineFailures = Selector('.jasmine-failures')

fixture `Jasmine`
    .page `http://localhost:5000`;

test("Connection Tests", async t => {
    await t.navigateTo("/connectionTests.html");
    let bar = await jasmineBar();

    if (bar.classNames.indexOf('jasmine-passed') == -1) {
        // The tests failed, find the results
        let failures = [];
        let failuresNode = await jasmineFailures();
        let childrenCount = failuresNode.childElementCount;
        for(let i = 0; i < childrenCount; i++) {
            let failureNode = await jasmineFailures.child(i).child('.jasmine-description').child('a');
            failures.push(await failureNode.innerText);
            console.log("FAILURE: " + failureNode.innerText);
        }

        await t.expect(failures).eql([]);
    }
});