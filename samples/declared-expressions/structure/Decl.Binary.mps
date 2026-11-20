<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d0000000-0000-4000-9000-000000000004" name="Decl.Binary">
  <nodes>
    <node id="d0000000-0000-4000-8000-000000000004" concept="Structure.Concept" name="Decl.Binary">
      <properties>
        <key>Decl.Binary</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>Two expressions and an operator between them.</description>
        <nameSlot></nameSlot>
        <nameRule>camel</nameRule>
      </properties>
      <children role="supertypes">
        <node id="d0000000-0000-4000-8000-000000000104" concept="Structure.SuperType" name="Decl.Expression">
          <properties>
            <conceptKey>Decl.Expression</conceptKey>
          </properties>
        </node>
      </children>
      <children role="properties">
        <node id="d0000000-0000-4000-8000-000000000204" concept="Structure.PropertyDecl" name="operator">
          <properties>
            <name>operator</name>
            <typeName>string</typeName>
            <defaultValue>+</defaultValue>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
      </children>
      <children role="children">
        <node id="d0000000-0000-4000-8000-000000000304" concept="Structure.ChildDecl" name="left">
          <properties>
            <name>left</name>
            <childConceptKey>Decl.Expression</childConceptKey>
            <cardinality>0..1</cardinality>
          </properties>
        </node>
        <node id="d0000000-0000-4000-8000-000000000305" concept="Structure.ChildDecl" name="right">
          <properties>
            <name>right</name>
            <childConceptKey>Decl.Expression</childConceptKey>
            <cardinality>0..1</cardinality>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>