<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="e002b1a4-5613-41d0-83bd-ad57ab85f1a6" name="release">
  <nodes>
    <node id="53230bb3-e53a-4fc8-be9b-b17ca83ec817" concept="Workflow.Workflow" name="Release">
      <properties>
        <name>Release</name>
      </properties>
      <children role="first">
        <node id="841f79f4-45e6-4773-8871-6944c2c0e29a" concept="Workflow.Step" name="build">
          <properties>
            <name>build</name>
            <command>dotnet build</command>
          </properties>
          <children role="next">
            <node id="ee194b90-b66f-4299-835c-c9821045d5b7" concept="Workflow.Step" name="test">
              <properties>
                <name>test</name>
                <command>dotnet test</command>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>