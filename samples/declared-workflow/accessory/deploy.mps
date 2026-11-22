<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b8000000-0000-4000-8008-000000000001" name="deploy">
  <nodes>
    <node id="b8000000-0000-4000-8008-000000000002" concept="Workflow.Workflow" name="deploy">
      <properties>
        <name>deploy</name>
      </properties>
      <children role="first">
        <node id="b8000000-0000-4000-8008-000000000003" concept="Workflow.Step" name="build">
          <properties>
            <name>build</name>
            <command>dotnet build</command>
          </properties>
          <children role="next">
            <node id="b8000000-0000-4000-8008-000000000004" concept="Workflow.Step" name="publish">
              <properties>
                <name>publish</name>
                <command>dotnet publish -c Release</command>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>