<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="6e1b04d9-4dce-4391-9d7d-d010abbc7d0a" name="draws">
  <nodes>
    <node id="460d6e1b-e790-4ce0-a835-4bf321390510" concept="Testing.Test" name="pipeline.mps">
      <properties>
        <document>pipeline.mps</document>
      </properties>
      <children role="assertion">
        <node id="9963bc71-cdd2-4797-8aee-3ea93c00d80a" concept="Testing.Draws">
          <properties>
            <width>60</width>
          </properties>
          <children role="lines">
            <node id="1a2b3c4d-5e6f-4a1b-8c2d-3e4f5a6b7c8d" concept="Testing.Line">
              <properties>
                <text>pipeline Release</text>
              </properties>
            </node>
            <node id="2b3c4d5e-6f70-4b2c-9d3e-4f5a6b7c8d9e" concept="Testing.Line">
              <properties>
                <text>  stage run          retries</text>
              </properties>
            </node>
            <node id="3c4d5e6f-7081-4c3d-ae4f-5a6b7c8d9e0f" concept="Testing.Line">
              <properties>
                <text>  build dotnet build 1</text>
              </properties>
            </node>
            <node id="4d5e6f70-8192-4d4e-bf50-6b7c8d9e0f1a" concept="Testing.Line">
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
