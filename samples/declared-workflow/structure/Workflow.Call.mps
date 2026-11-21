<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="6d5b9e0e-1c4c-4a35-8f3e-2a7d1c6b4e21" name="definition3">
  <nodes>
    <node id="a1f0c0de-7c6e-4f0a-9a3e-5b6c8d7e2f10" concept="Structure.Concept" name="Workflow.Call">
      <properties>
        <key>Workflow.Call</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isRoot>true</isRoot>
        <alias></alias>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>Runs another step of this workflow here, instead of a command of its own.</description>
        <nameSlot>name</nameSlot>
      </properties>
      <children role="supertypes">
        <node id="b2c1d3e4-f5a6-4b70-8c91-0d2e3f4a5b60" concept="Structure.SuperType" name="Workflow.Step">
          <properties>
            <conceptKey>Workflow.Step</conceptKey>
          </properties>
        </node>
      </children>
      <children role="references">
        <node id="c3d4e5f6-a7b8-4c91-8da2-1e3f4a5b6c71" concept="Structure.ReferenceDecl" name="calls">
          <properties>
            <name>calls</name>
            <targetConceptKey>Workflow.Step</targetConceptKey>
            <allowOwnSubtree>false</allowOwnSubtree>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>