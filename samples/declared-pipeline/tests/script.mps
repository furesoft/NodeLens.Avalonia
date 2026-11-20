<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="771659d8-9b86-48d1-9af3-579e30d79618" name="script">
  <nodes>
    <node id="9b3290e8-6d19-4298-8a02-85e3044e08dc" concept="Testing.Test" name="pipeline.mps">
      <properties>
        <document>pipeline.mps</document>
      </properties>
      <children role="assertion">
        <node id="5e83ff12-28cd-46d5-87b0-a57144a11d19" concept="Testing.Script">
          <properties>
            <width>60</width>
          </properties>
          <children role="steps">
            <node id="72003b98-bd22-468f-87d6-dcc1400b6351" concept="Testing.Caret">
              <properties>
                <at>Release</at>
              </properties>
            </node>
            <node id="e2729e39-7b10-4242-966c-8c79ce41bef5" concept="Testing.Type">
              <properties>
                <typed>Pre</typed>
              </properties>
            </node>
          </children>
          <children role="lines">
            <node id="66a74269-1637-44a7-b9a0-3fa3c82cdd05" concept="Testing.Line">
              <properties>
                <text>pipeline PreRelease</text>
              </properties>
            </node>
            <node id="c1882de8-9332-4273-a61c-b6188d20c6fd" concept="Testing.Line">
              <properties>
                <text>  stage run          retries</text>
              </properties>
            </node>
            <node id="8f32c7c8-b14f-4ec9-8c89-104fc85cc2d9" concept="Testing.Line">
              <properties>
                <text>  build dotnet build 1</text>
              </properties>
            </node>
            <node id="2cbcc0a9-6de2-460f-afcb-b31529db1da3" concept="Testing.Line">
              <properties>
                <text>  test  dotnet test  0</text>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>
