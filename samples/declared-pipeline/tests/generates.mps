<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="22af37cc-1754-4e27-a733-ac7704663443" name="generates">
  <nodes>
    <node id="484d947e-6e8f-4382-8366-14497012aadc" concept="Testing.Test" name="pipeline.mps">
      <properties>
        <document>pipeline.mps</document>
      </properties>
      <children role="assertion">
        <node id="b032fafe-4b20-4a2f-a81f-93b92298d45f" concept="Testing.Generates">
          <properties>
            <generator>ShellScript</generator>
          </properties>
          <children role="lines">
            <node id="3fcdf94b-6033-49cb-aa85-bb5ed68eec8d" concept="Testing.Line">
              <properties>
                <text># Release</text>
              </properties>
            </node>
            <node id="5a1c8f2b-7d3e-4b60-9c11-2e4f6a8b0d13" concept="Testing.Line">
              <properties>
                <text>dotnet build</text>
              </properties>
            </node>
            <node id="6b2da03c-8e4f-4c71-ad22-3f50719c1e24" concept="Testing.Line">
              <properties>
                <text>dotnet test</text>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>
