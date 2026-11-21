<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="ae488c45-706b-475f-a64f-e18f1f7af15a" name="definition2">
  <nodes>
    <node id="1befa4d7-97fb-46af-bae4-52a25a5353ff" concept="Structure.Concept" name="Workflow.Step">
      <properties>
        <key>Workflow.Step</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isRoot>true</isRoot>
        <alias></alias>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>One step of a workflow; the next step and the one to repeat on failure.</description>
        <nameSlot>name</nameSlot>
      </properties>
      <children role="properties">
        <node id="500a04f7-1fe5-453d-a3d4-4f61aa3669e8" concept="Structure.PropertyDecl" name="name">
          <properties>
            <name>name</name>
            <typeName>string</typeName>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
        <node id="ca0f6847-2744-4f42-b734-7b6a829392f0" concept="Structure.PropertyDecl" name="command">
          <properties>
            <name>command</name>
            <typeName>string</typeName>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
      </children>
      <children role="children">
        <node id="038c4ff8-b5b6-470c-87ee-6ea85dd5fc72" concept="Structure.ChildDecl" name="next">
          <properties>
            <name>next</name>
            <childConceptKey>Workflow.Step</childConceptKey>
            <cardinality>0..1</cardinality>
          </properties>
        </node>
      </children>
      <children role="references">
        <node id="b674e8b2-ce81-4e74-be98-8e6c5515ab73" concept="Structure.ReferenceDecl" name="onFailure">
          <properties>
            <name>onFailure</name>
            <targetConceptKey>Workflow.Step</targetConceptKey>
            <allowOwnSubtree>false</allowOwnSubtree>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>